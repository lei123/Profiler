﻿using System.Collections.Generic;
using Caliburn.PresentationFramework.ApplicationModel;
using Caliburn.PresentationFramework.Screens;
using ExceptionHandler;
using Machine.Specifications;
using NServiceBus.Profiler.Common.ExtensionMethods;
using NServiceBus.Profiler.Common.Plugins;
using NServiceBus.Profiler.Core;
using NServiceBus.Profiler.Desktop.About;
using NServiceBus.Profiler.Desktop.Explorer;
using NServiceBus.Profiler.Desktop.MessageList;
using NServiceBus.Profiler.Desktop.ScreenManager;
using NServiceBus.Profiler.Desktop.Shell;
using NSubstitute;
using System.Linq;

namespace NServiceBus.Profiler.Tests.Shell
{
    public class TestPlugin : Screen, IPlugin
    {
        public IList<PluginContextMenu> ContextMenuItems { get; set; }
        public int TabOrder { get { return 1; } }
    }

    [Subject("shell")]
    public class with_a_shell
    {
        protected static ShellViewModel shell;
        protected static IScreenFactory screenFactory;
        protected static IWindowManagerEx windowManager;
        protected static IExplorerViewModel explorer;
        protected static IMessageListViewModel messageList;
        protected static AboutViewModel aboutViewModel;
        protected static ConnectToMachineViewModel connectToViewModel;
        protected static INetworkOperations networkOperations;
        protected static IEventAggregator eventAggregator;
        protected static IExceptionHandler exceptionHandler;
        protected static IStatusBarManager statusbarManager;
        protected static IList<IPlugin> plugins;
        protected static IPlugin fakePlugin;
            
        Establish context = () =>
        {
            screenFactory = Substitute.For<IScreenFactory>();
            windowManager = Substitute.For<IWindowManagerEx>();
            explorer = Substitute.For<IExplorerViewModel>();
            messageList = Substitute.For<IMessageListViewModel>();
            networkOperations = Substitute.For<INetworkOperations>();
            exceptionHandler = Substitute.For<IExceptionHandler>();
            statusbarManager = Substitute.For<IStatusBarManager>();
            eventAggregator = Substitute.For<IEventAggregator>();
            aboutViewModel = Substitute.For<AboutViewModel>(networkOperations);
            connectToViewModel = Substitute.For<ConnectToMachineViewModel>(networkOperations);
            fakePlugin = Substitute.For<IPlugin>();
            screenFactory.CreateScreen<AboutViewModel>().Returns(aboutViewModel);
            screenFactory.CreateScreen<ConnectToMachineViewModel>().Returns(connectToViewModel);
            plugins = new List<IPlugin>(new[] { fakePlugin });
            shell = new ShellViewModel(screenFactory, windowManager, explorer, messageList, statusbarManager, eventAggregator, plugins);
        };

        Cleanup after = () => ((IScreen)shell).Deactivate(true);
    }

    public class when_there_is_work_in_progress : with_a_shell
    {
        Because of = () => shell.WorkInProgress = true;

        It should_not_allow_deleting_the_queue = () => shell.CanDeleteCurrentQueue().ShouldBeFalse();
        It should_not_allow_deleting_the_messages = () => shell.CanDeleteSelectedMessages().ShouldBeFalse();
        It should_not_allow_connecting_to_another_machine = () => shell.CanConnectToMachine().ShouldBeFalse();
        It should_not_allow_creating_new_messages = () => shell.CanCreateMessage().ShouldBeFalse();
        It should_not_allow_creating_new_queue = () => shell.CanCreateQueue().ShouldBeFalse();
        It should_not_allow_exporting_messages = () => shell.CanExportMessage().ShouldBeFalse();
        It should_not_allow_importing_messages = () => shell.CanImportMessage().ShouldBeFalse();
        It should_not_allow_purging_the_queue = () => shell.CanPurgeCurrentQueue().ShouldBeFalse();
        It should_not_allow_refreshing_the_queue = () => shell.CanRefreshQueues().ShouldBeFalse();
    }

    public class when_displaying_about_information : with_a_shell
    {
        Because of = () => shell.ShowAbout();

        It should_display_about_information = () =>
        {
            screenFactory.Received().CreateScreen<AboutViewModel>();
            windowManager.Received().ShowDialog(aboutViewModel);
        };
    }

    public class when_connecting_to_msmq : with_a_shell
    {
        Because of = () =>
        {
            connectToViewModel.ComputerName.Returns("NewMachine");
            windowManager.ShowDialog(Arg.Any<object>(), Arg.Any<object>()).Returns(true);

            shell.ConnectToMachine();
        };

        It should_display_connect_dialog = () =>
        {
            screenFactory.Received().CreateScreen<ConnectToMachineViewModel>();
            windowManager.Received().ShowDialog(connectToViewModel);
        };

        It should_connect_explorer_to_the_msmq_machine = () => explorer.Received().ConnectToQueue("NewMachine");

    }

    public class when_shell_activated : with_a_shell
    {
        Because of = () => ((IScreen)shell).Activate();

        It should_have_plugins_loaded = () => shell.Items.Count(x => x is IPlugin).ShouldEqual(1);
        It should_have_all_child_screens_ready = () => shell.Items.ForEach(x => x.Received().Activate());
        It should_have_all_plugin_screens_loaded = () => shell.Plugins.ForEach(x => x.Received().Activate());
    }

    public class when_turning_off_auto_refresh : with_a_shell
    {
        Establish context = () => shell.AutoRefreshQueues = false;

        Because of = () => shell.OnAutoRefreshing();

        It should_not_refresh_the_queues = () => explorer.DidNotReceive().RefreshMessageCount();
    }
}