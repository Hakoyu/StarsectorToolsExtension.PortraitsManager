using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HKW.ViewModels.Controls;
using StarsectorTools.Libs.GameInfo;
using StarsectorTools.Libs.Utils;
using StarsectorToolsExtension.PortraitsManager.Models;

namespace StarsectorToolsExtension.PortraitsManager.ViewModels
{
    internal partial class PortraitsManagerViewModel : ObservableObject
    {
        [ObservableProperty]
        private string maleFilterText;

        [ObservableProperty]
        private string femaleFilterText;

        [ObservableProperty]
        private bool isRemindSave = false;

        [ObservableProperty]
        ComboBoxVM comboBox_GroupList =
            new()
            {
                new() { Content = "原版", Tag = "Vanilla" },
                new() { Content = "已启用模组", Tag = nameof(ModTypeGroup.Enabled) },
                new() { Content = "已收藏模组", Tag = nameof(ModTypeGroup.Collected) }
            };

        [ObservableProperty]
        ObservableCollection<GroupData> mainModsList = new();

        [ObservableProperty]
        private ObservableCollection<ListBoxItemVM> nowMalePortraitItems;

        [ObservableProperty]
        private ObservableCollection<ListBoxItemVM> nowFemalePortraitItems;


        private ListBoxItemVM nowSelectedFactionItem;
        public PortraitsManagerViewModel() { }

        public PortraitsManagerViewModel(bool noop)
        {
            ComboBox_GroupList.SelectionChangedEvent += ComboBox_GroupList_SelectionChangedEvent;
            ComboBox_GroupList.SelectedIndex = 0;
            InitializeGroup();
        }

        private void ComboBox_GroupList_SelectionChangedEvent(ComboBoxItemVM item)
        {
            ChangeAllGroupData(item.Tag!.ToString()!);
        }

        private void InitializeGroup()
        {
            GetAllUserGroup();
        }

        private void GetAllUserGroup()
        {
            //foreach (var group in ModsInfo.AllUserGroups)
            //{
            //    ComboBox_GroupList.Add(new()
            //    {
            //        Content = group.Key,
            //        Tag = group.Key
            //    });
            //}
        }

        private void ChangeAllGroupData(string groupTypeName)
        {
            MainModsList.Clear();
            if (groupTypeName == strVanilla)
            {
                MainModsList.Add(GetVanillaData());
            }
        }

        private GroupData GetVanillaData()
        {
            var groupData = new GroupData(strVanilla, "原版", GameInfo.CoreDirectory);
            groupData.FactionList.SelectionChangedEvent += FactionList_SelectionChangedEvent;
            return groupData;
        }

        private void FactionList_SelectionChangedEvent(ListBoxItemVM item)
        {
            if (item is null || nowSelectedFactionItem == item)
                return;
            // 若切换选择,可取消原来的选中状态,以此达到多列表互斥
            if (nowSelectedFactionItem?.IsSelected is true)
                nowSelectedFactionItem.IsSelected = false;
            nowSelectedFactionItem = item;
            if (nowSelectedFactionItem.Tag is not GroupData groupData)
                return;
            NowMalePortraitItems = groupData.MaleFactionPortraitsItem[nowSelectedFactionItem.Name!];
            NowFemalePortraitItems = groupData.FemaleFactionPortraitsItem[nowSelectedFactionItem.Name!];
        }

        [RelayCommand]
        internal void Save() { }

        [RelayCommand]
        private void MaleFilterTextChange(string value) { }

        [RelayCommand]
        private void FemaleFilterTextChange(string value) { }
    }
}
