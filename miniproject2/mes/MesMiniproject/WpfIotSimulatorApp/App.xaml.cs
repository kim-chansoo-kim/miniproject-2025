﻿using System.Windows;
using WpfIotSimulatorApp.Views;
using WpfIotSimulatorApp.ViewModels;

namespace WpfIotSimulatorApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            var viewModel = new MainViewModel();
            var view = new MainView
            {
                DataContext = viewModel,
            };

            viewModel.StartHmiRequested += view.StartHmiAni;
            viewModel.StartSensorCheckRequested += view.StartSensorCheck;
            view.ShowDialog();
        }
    }
}
