using System;
using System.Collections;
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
        private string _maleFilterText;

        [ObservableProperty]
        private string _femaleFilterText;

        [ObservableProperty]
        private bool _isRemindSave = false;

        [ObservableProperty]
        ComboBoxVM _comboBox_GroupList =
            new()
            {
                new() { Content = "原版", Tag = "Vanilla" },
                new() { Content = "已启用模组", Tag = nameof(ModTypeGroup.Enabled) },
                new() { Content = "已收藏模组", Tag = nameof(ModTypeGroup.Collected) }
            };

        [ObservableProperty]
        private ObservableCollection<GroupData> _allGroupDatas = new();

        private GroupData _nowGroupData;

        [ObservableProperty]
        private ObservableCollection<ListBoxItemVM> _nowShowMalePortraitItems;

        [ObservableProperty]
        private ObservableCollection<ListBoxItemVM> _nowShowFemalePortraitItems;


        private ListBoxItemVM _nowSelectedFactionItem;

        internal List<ListBoxItemVM> NowSelectedMalePortraitItems { get; private set; }
        internal List<ListBoxItemVM> NowSelectedFemalePortraitItems { get; private set; }
        public PortraitsManagerViewModel() { }

        public PortraitsManagerViewModel(bool noop)
        {
            Instance = this;
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
            AllGroupDatas.Clear();
            if (groupTypeName == strVanilla)
            {
                var groupData = GetVanillaData();
                AllGroupDatas.Add(groupData);
                groupData.IsExpanded = true;
            }
        }

        private GroupData GetVanillaData()
        {
            if (GroupData.Create(strVanilla, "原版", GameInfo.CoreDirectory) is not GroupData groupData)
                return null!;
            groupData.FactionList.SelectionChangedEvent += FactionList_SelectionChangedEvent;
            return groupData;
        }

        private void FactionList_SelectionChangedEvent(ListBoxItemVM item)
        {
            if (item is null || _nowSelectedFactionItem == item)
                return;
            // 若切换选择,可取消原来的选中状态,以此达到多列表互斥
            if (_nowSelectedFactionItem?.IsSelected is true)
                _nowSelectedFactionItem.IsSelected = false;
            _nowSelectedFactionItem = item;
            if (_nowSelectedFactionItem.Tag is not GroupData groupData)
                return;
            _nowGroupData = groupData;
            NowShowMalePortraitItems = groupData.MaleFactionPortraitsItem[_nowSelectedFactionItem.Name!];
            NowShowFemalePortraitItems = groupData.FemaleFactionPortraitsItem[_nowSelectedFactionItem.Name!];
        }

        [RelayCommand]
        internal void Save()
        {
            foreach (var groupData in AllGroupDatas)
                groupData.Save();
            IsRemindSave = false;
        }

        [RelayCommand]
        private void MaleFilterTextChange(string value) { }

        [RelayCommand]
        private void FemaleFilterTextChange(string value) { }

        [RelayCommand]
        private void MaleSelectionChanged(IList values)
        {
            NowSelectedMalePortraitItems = new(values.OfType<ListBoxItemVM>());
        }

        [RelayCommand]
        private void FemaleSelectionChanged(IList values)
        {
            NowSelectedFemalePortraitItems = new(values.OfType<ListBoxItemVM>());
        }
    }
}
