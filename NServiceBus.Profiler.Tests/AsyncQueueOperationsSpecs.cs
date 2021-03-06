﻿using System;
using System.Threading.Tasks;
using Machine.Specifications;
using NServiceBus.Profiler.Common.Models;
using NServiceBus.Profiler.Core;

namespace NServiceBus.Profiler.Tests.Messages
{
    [Subject("queues")]
    public class with_messages_in_the_queue
    {
        protected static Queue SourceQ;
        protected static Queue DestinationQ;
        protected static IQueueManagerAsync Manager;
        protected static Task Task;

        Establish context = () =>
        {
            Manager = new AsyncQueueManager(new MSMQueueOperations());
            SourceQ = Manager.CreatePrivateQueue(new Queue(Guid.NewGuid().ToString("N")));
            DestinationQ = Manager.CreatePrivateQueue(new Queue(Guid.NewGuid().ToString("N")));
        };

        Because of = () =>
        {
            for (var i = 0; i < 5000; i++)
            {
                Manager.SendMessage(DestinationQ, string.Format("Test message number {0}, this is a somewhat larger text message. this is a somewhat larger text message. this is a somewhat larger text message. this is a somewhat larger text message.", i));
            }
        };

        It should_be_able_to_load_messages_from_the_queue = () => Manager.GetMessages(DestinationQ).Result.Count.ShouldEqual(5000);

        It should_not_block_ui_when_loading_messages_asynchronously = () =>
        {
            Task = Manager.GetMessages(DestinationQ);
            (Task.Status == TaskStatus.Running || Task.Status == TaskStatus.WaitingToRun).ShouldBeTrue();
        };

        Cleanup after = () =>
        {
            Task.ContinueWith(x =>
            {
                Manager.DeleteQueue(SourceQ);
                Manager.DeleteQueue(DestinationQ);
            });
        };
    }
}