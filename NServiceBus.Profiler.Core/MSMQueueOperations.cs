﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Messaging;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using NServiceBus.Profiler.Common;
using NServiceBus.Profiler.Common.ExtensionMethods;
using NServiceBus.Profiler.Common.Models;
using log4net;

namespace NServiceBus.Profiler.Core
{
    public class MSMQueueOperations : IQueueOperationsAsync
    {
        private readonly IMapper _mapper;

        public MSMQueueOperations() : this(new DefaultMapper())
        {
        }

        public MSMQueueOperations(IMapper mapper)
        {
            _mapper = mapper;
        }

        public ILog Logger { get; set; }

        public virtual IList<Queue> GetQueues(string machineName)
        {
            var queuesPrivate = MessageQueue.GetPrivateQueuesByMachine(machineName).ToList();
            var mapped = queuesPrivate.Select(x => _mapper.MapQueue(x)).ToList();

            queuesPrivate.ForEach(x => x.Close());

            return mapped;
        }

        public virtual Task<IList<MessageInfo>> GetMessagesAsync(Queue queue)
        {
            return Task.Run(() => GetMessages(queue));
        }

        public virtual IList<MessageInfo> GetMessages(Queue queue)
        {
            using (var q = queue.AsMessageQueue())
            {
                q.MessageReadPropertyFilter.ClearAll();
                q.MessageReadPropertyFilter.Id = true;
                q.MessageReadPropertyFilter.Label = true;
                q.MessageReadPropertyFilter.SentTime = true;

                var result = new List<MessageInfo>();
                if (!queue.CanRead)
                {
                    return result;
                }

                var msgLoop = q.GetMessageEnumerator2();
                try
                {
                    while (msgLoop.MoveNext())
                    {
                        try
                        {
                            var currentMessage = msgLoop.Current;
                            if (currentMessage != null)
                            {
                                result.Add(_mapper.MapInfo(currentMessage));
                            }
                        }
                        catch (MessageQueueException ex)
                        {
                            Logger.Error("There was an error reading message from the queue.", ex);
                        }
                    }
                }
                finally
                {
                    msgLoop.Close();
                }

                return result;
            }
        }

        public MessageBody GetMessageBody(Queue queue, string messageId)
        {
            using (var q = queue.AsMessageQueue(QueueAccessMode.SendAndReceive))
            {
                q.MessageReadPropertyFilter.SetAll();
                q.MessageReadPropertyFilter.SourceMachine = true;

                try
                {
                    return _mapper.MapBody(q.PeekById(messageId));
                }
                catch (InvalidOperationException) //message is removed from the queue (by another process)
                {
                    return null;
                }
            }
        }

        private bool QueueExists(Queue queue)
        {
            var allQueues = GetQueues(queue.Address.Machine);
            return allQueues.Any(q => q.Address.Queue.Contains(queue.Address.Queue, StringComparison.InvariantCultureIgnoreCase));
        }

        public virtual Queue CreateQueue(Queue queue, bool transactional)
        {
            var path = queue.Address.ToPathName();
            if(!QueueExists(queue)) //MessageQueue.Exist method does not accept format name
            {
                var q = MessageQueue.Create(queue.Address.ToShortFormatName(), transactional);
                return _mapper.MapQueue(q);
            }
            else
            {
                return _mapper.MapQueue(queue.AsMessageQueue());
            }
        }

        public virtual void DeleteQueue(Queue queue)
        {
            var format = queue.Address.ToFormatName();

            MessageQueue.Delete(format);
        }

        public virtual void Send(Queue queue, object message)
        {
            using(var q = queue.AsMessageQueue(QueueAccessMode.SendAndReceive))
            {
                q.Send(message, MessageQueueTransactionType.Single);
            }
        }

        public virtual void DeleteMessage(Queue queue, string messageId)
        {
            using(var q = queue.AsMessageQueue(QueueAccessMode.SendAndReceive))
            {
                q.ReceiveById(messageId);
            }
        }

        public virtual void PurgeQueue(Queue queue)
        {
            using(var q = queue.AsMessageQueue(QueueAccessMode.ReceiveAndAdmin))
            {
                q.Purge();
            }
        }

        public virtual void MoveMessage(Queue source, Queue destination, string messageId)
        {
            using(var tx = new TransactionScope())
            using(var queueDest = destination.AsMessageQueue(QueueAccessMode.SendAndReceive))
            using(var queueSource = source.AsMessageQueue(QueueAccessMode.SendAndReceive))
            {
                Guard.True(queueSource.CanRead, () => new QueueManagerException(string.Format("Can not read messages from queue {0}", source.Address)));
                Guard.True(queueSource.Transactional, () => new QueueManagerException(string.Format("Queue {0} is not transactional", source.Address)));
                
                Guard.True(queueDest.CanRead, () => new QueueManagerException(string.Format("Can not read messages from queue {0}", destination.Address)));
                Guard.True(queueDest.Transactional, () => new QueueManagerException(string.Format("Queue {0} is not transactional", destination.Address)));

                Func<string, Message> retreival = x => queueSource.ReceiveById(x, MessageQueueTransactionType.Automatic);
                
                var msg = retreival.TryGetValue(messageId);

                Guard.NotNull(() => msg, msg, () => new QueueManagerException("Message could not be loaded."));

                queueDest.Send(msg, MessageQueueTransactionType.Automatic);

                tx.Complete();
            }
        }

        public virtual int GetMessageCount(Queue queue)
        {
            var messageCount = 0;

            using (var q = queue.AsMessageQueue())
            {
                q.MessageReadPropertyFilter.ClearAll();
                var msgLoop = q.GetMessageEnumerator2();

                if (queue.CanRead)
                {
                    while (msgLoop.MoveNext())
                    {
                        messageCount++;
                    }
                }
            }
            return messageCount;
        }
    }
}