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
using StarsectorTools.Libs.Utils;
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

        public PortraitsManagerPage()
        {
            InitializeComponent();
            DataContext = new PortraitsManagerViewModel(true);
            //ComboBox_GroupList.SelectedIndex = 0;
        }

        private void ComboBox_GroupList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item)
            {
                ChangeGroupsType(item.Tag.ToString()!);
            }
        }

        private void TextBox_MaleSearchPortraits_TextChanged(object sender, TextChangedEventArgs e)
        {
            MaleSearchPortraits(nowGroup, nowFaction);
        }


        private void TextBox_FemaleSearchPortraits_TextChanged(object sender, TextChangedEventArgs e)
        {
            FemaleSearchPortraits(nowGroup, nowFaction);
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }

        private void ListBox_MalePortraitsList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is Array pathArray)
            {
                if (string.IsNullOrEmpty(nowFaction))
                {
                    MessageBoxVM.Show(new("必须选择势力"));
                    return;
                }
                foreach (string path in pathArray)
                {
                    if (File.Exists(path))
                        DropFile(path, Gender.Male);
                    else if (Directory.Exists(path))
                        DropDirectory(path, Gender.Male);
                }
                RefreshPortraits(nowGroup, nowFaction, Gender.Male);
                RefreshGroupImagesCount(nowGroup);
            }
        }

        private void ListBox_FemalePortraitsList_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is Array pathArray)
            {
                if (string.IsNullOrEmpty(nowFaction))
                {
                    MessageBoxVM.Show(new("必须选择势力"));
                    return;
                }
                foreach (string path in pathArray)
                {
                    if (File.Exists(path))
                        DropFile(path, Gender.Male);
                    else if (Directory.Exists(path))
                        DropDirectory(path, Gender.Male);
                }
                RefreshPortraits(nowGroup, nowFaction, Gender.Female);
                RefreshGroupImagesCount(nowGroup);
            }
        }

        public string GetNameI18n()
        {
            return "肖像管理器";
        }

        public string GetDescriptionI18n()
        {
            return "";
        }
    }
}
