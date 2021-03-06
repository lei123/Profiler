﻿using System.Collections.Generic;
using Caliburn.PresentationFramework.Filters;
using Caliburn.PresentationFramework.Screens;
using NServiceBus.Profiler.Common.Models;
using NServiceBus.Profiler.Core;

namespace NServiceBus.Profiler.Desktop.Shell
{
    public class ConnectToMachineViewModel : Screen
    {
        private readonly INetworkOperations _networkOperations;

        public ConnectToMachineViewModel(INetworkOperations networkOperations)
        {
            _networkOperations = networkOperations;
            Machines = new List<string>();
            DisplayName = "Connect To Queue";
        }

        protected override void OnActivate()
        {
            base.OnActivate();

            Machines.Clear();
            Machines.AddRange(_networkOperations.GetMachines());
            IsAddressValid = true;
        }

        public virtual string ComputerName { get; set; }

        public virtual void Close()
        {
            TryClose(false);
        }

        public virtual bool CanAccept()
        {
            return !string.IsNullOrEmpty(ComputerName);
        }

        public List<string> Machines { get; private set; }

        public bool IsAddressValid { get; set; }

        [AutoCheckAvailability]
        public virtual void Accept()
        {
            IsAddressValid = Address.IsValidAddress(ComputerName);
            if (IsAddressValid)
            {
                TryClose(true);
            }
        }
    }
}