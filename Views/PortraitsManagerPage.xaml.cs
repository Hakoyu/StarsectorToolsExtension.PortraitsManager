using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.ComponentModel;
using HKW.ViewModels.Dialogs;
using HKW.WPF;
using StarsectorTools.Libs.Utils;
using StarsectorToolsExtension.PortraitsManager.Models;
using StarsectorToolsExtension.PortraitsManager.ViewModels;

namespace StarsectorToolsExtension.PortraitsManager.Views
{
    /// <summary>
    /// PortraitsManagerPage.xaml 的交互逻辑
    /// </summary>
    public partial class PortraitsManagerPage : Page, ISTPage
    {
        internal PortraitsManagerViewModel ViewModel => (PortraitsManagerViewModel)DataContext;

        public bool NeedSave => ViewModel.IsRemindSave;

        private ScrollViewer _malePortraitListBoxScrollViewer;

        private ScrollViewer _femalePortraitListBoxScrollViewer;

        public PortraitsManagerPage()
        {
            InitializeComponent();
            DataContext = new PortraitsManagerViewModel(true);
        }

        private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = MouseWheelEvent,
                Source = sender,
            };
            if (sender is Control control && control.Parent is UIElement ui)
                ui.RaiseEvent(eventArg);
            e.Handled = true;
        }

        private void ListBox_MalePortraitsList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is Array pathArray)
            {
                ViewModel.DropPortraitFiles(pathArray, Gender.Male);
            }
        }

        private void ListBox_FemalePortraitsList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is Array pathArray)
            {
                ViewModel.DropPortraitFiles(pathArray, Gender.Female);
            }
        }

        public string GetNameI18n()
        {
            return "肖像管理器";
        }

        public string GetDescriptionI18n()
        {
            return "肖像管理器";
        }

        public void Save()
        {
            ViewModel.Save();
        }

        public void Close()
        {
            ViewModel.Close();
        }

        private void ListBox_MalePortraitsList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            _malePortraitListBoxScrollViewer ??= WPFUtils.FindVisualChild<ScrollViewer>((ListBox)sender)!;
            _malePortraitListBoxScrollViewer.ScrollToVerticalOffset(_malePortraitListBoxScrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void ListBox_FemalePortraitsList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            _femalePortraitListBoxScrollViewer ??= WPFUtils.FindVisualChild<ScrollViewer>((ListBox)sender)!;
            _femalePortraitListBoxScrollViewer.ScrollToVerticalOffset(_femalePortraitListBoxScrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }
}
