using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HKW.Libs.Log4Cs;
using HKW.ViewModels.Controls;
using HKW.ViewModels.Dialogs;
using StarsectorTools.Libs.GameInfo;
using StarsectorTools.Libs.Utils;
using StarsectorToolsExtension.PortraitsManager.Models;

namespace StarsectorToolsExtension.PortraitsManager.ViewModels
{
    internal partial class PortraitsManagerViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _maleGroupBoxHeader = "男性肖像";

        [ObservableProperty]
        private string _femaleGroupBoxHeader = "女性肖像";

        [ObservableProperty]
        private string _malePortraitFilterText;

        partial void OnMalePortraitFilterTextChanged(string value) => MalePortraitFilter(value);

        [ObservableProperty]
        private string _femalePortraitFilterText;

        partial void OnFemalePortraitFilterTextChanged(string value) => FemalePortraitFilter(value);

        [ObservableProperty]
        private bool _isRemindSave = false;

        [ObservableProperty]
        ComboBoxVM _comboBox_GroupList =
            new()
            {
                new() { Content = "原版", Tag = _StrVanilla },
                new() { Content = "已启用模组", Tag = nameof(ModTypeGroup.Enabled) },
                new() { Content = "已收藏模组", Tag = nameof(ModTypeGroup.Collected) }
            };

        [ObservableProperty]
        private ObservableCollection<GroupData> _allGroupDatas = new();

        private GroupData _nowGroupData;

        [ObservableProperty]
        private ObservableCollection<ListBoxItemVM> _nowShowMalePortraitItems;

        partial void OnNowShowMalePortraitItemsChanged(ObservableCollection<ListBoxItemVM> value)
        {
            MaleGroupBoxHeader = $"男性肖像 ({NowShowMalePortraitItems.Count})";
        }

        [ObservableProperty]
        private ObservableCollection<ListBoxItemVM> _nowShowFemalePortraitItems;

        partial void OnNowShowFemalePortraitItemsChanged(ObservableCollection<ListBoxItemVM> value)
        {
            FemaleGroupBoxHeader = $"女性肖像 ({NowShowFemalePortraitItems.Count})";
        }

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
            foreach (var group in ModInfos.AllUserGroups)
            {
                ComboBox_GroupList.Add(new() { Content = group.Key, Tag = group.Key });
            }
        }

        #region ChangeAllGroupData
        private void ChangeAllGroupData(string groupTypeName)
        {
            CheckRemindSave();
            Close();
            AllGroupDatas.Clear();
            if (groupTypeName == _StrVanilla)
            {
                var groupData = GetGroupData(_StrVanilla, "原版", GameInfo.CoreDirectory)!;
                AllGroupDatas.Add(groupData);
                groupData.IsExpanded = true;
            }
            else if (groupTypeName == nameof(ModTypeGroup.Enabled))
            {
                GetEnabledModsGroupData();
            }
            else if (groupTypeName == nameof(ModTypeGroup.Collected))
            {
                GetCollectedModsGroupData();
            }
            else
            {
                GetUserGroupGroupData(groupTypeName);
            }
            GC.Collect();
        }

        private void CheckRemindSave()
        {
            if (!IsRemindSave)
                return;
            if (
                MessageBoxVM.Show(
                    new("你有未保存的数据, 需要保存吗?")
                    {
                        Icon = MessageBoxVM.Icon.Question,
                        Button = MessageBoxVM.Button.YesNo
                    }
                )
                is not MessageBoxVM.Result.Yes
            )
                return;
            Save();
        }

        private void GetEnabledModsGroupData()
        {
            foreach (var modId in ModInfos.AllEnabledModIds)
            {
                var modInfo = ModInfos.AllModInfos[modId];
                if (
                    GetGroupData(modInfo.Id, modInfo.Name, modInfo.ModDirectory)
                    is not GroupData groupData
                )
                    continue;
                AllGroupDatas.Add(groupData);
            }
        }

        private void GetCollectedModsGroupData()
        {
            foreach (var modId in ModInfos.AllCollectedModIds)
            {
                var modInfo = ModInfos.AllModInfos[modId];
                if (
                    GetGroupData(modInfo.Id, modInfo.Name, modInfo.ModDirectory)
                    is not GroupData groupData
                )
                    continue;
                AllGroupDatas.Add(groupData);
            }
        }

        private void GetUserGroupGroupData(string userGroup)
        {
            foreach (var modId in ModInfos.AllUserGroups[userGroup])
            {
                var modInfo = ModInfos.AllModInfos[modId];
                if (
                    GetGroupData(modInfo.Id, modInfo.Name, modInfo.ModDirectory)
                    is not GroupData groupData
                )
                    continue;
                AllGroupDatas.Add(groupData);
            }
        }

        private GroupData? GetGroupData(string groupId, string groupName, string baseDirectory)
        {
            if (GroupData.Create(groupId, groupName, baseDirectory) is not GroupData groupData)
                return null!;
            groupData.FactionList.SelectionChangedEvent += FactionList_SelectionChangedEvent;
            return groupData;
        }

        #region PortraitFilter
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
            MalePortraitFilterText = string.Empty;
            FemalePortraitFilterText = string.Empty;
            NowShowMalePortraitItems = groupData.MaleFactionPortraitsItem[
                _nowSelectedFactionItem.Id!
            ];
            NowShowFemalePortraitItems = groupData.FemaleFactionPortraitsItem[
                _nowSelectedFactionItem.Id!
            ];
            NowShowMalePortraitItems.CollectionChanged += (s, e) =>
                MalePortraitFilter(MalePortraitFilterText);
            NowShowFemalePortraitItems.CollectionChanged += (s, e) =>
                FemalePortraitFilter(FemalePortraitFilterText);
            Logger.Info($"切换至 分组: {_nowGroupData.Header} 势力: {_nowSelectedFactionItem.Name}");
        }

        private void MalePortraitFilter(string filterText)
        {
            if (_nowGroupData is null)
                return;
            if (string.IsNullOrWhiteSpace(filterText))
            {
                NowShowMalePortraitItems = _nowGroupData.MaleFactionPortraitsItem[
                    _nowSelectedFactionItem.Id!
                ];
            }
            else
            {
                NowShowMalePortraitItems = new(
                    _nowGroupData.MaleFactionPortraitsItem[_nowSelectedFactionItem.Id!].Where(
                        i =>
                            i.Name!
                                .ToString()!
                                .Contains(filterText, StringComparison.OrdinalIgnoreCase)
                    )
                );
                Logger.Info($"男性肖像列表搜索: {filterText}");
            }
        }

        private void FemalePortraitFilter(string filterText)
        {
            if (_nowGroupData is null)
                return;
            if (string.IsNullOrWhiteSpace(filterText))
            {
                NowShowFemalePortraitItems = _nowGroupData.FemaleFactionPortraitsItem[
                    _nowSelectedFactionItem.Id!
                ];
            }
            else
            {
                NowShowFemalePortraitItems = new(
                    _nowGroupData.FemaleFactionPortraitsItem[_nowSelectedFactionItem.Id!].Where(
                        i =>
                            i.Content!
                                .ToString()!
                                .Contains(filterText, StringComparison.OrdinalIgnoreCase)
                    )
                );
                Logger.Info($"女性肖像列表搜索: {filterText}");
            }
        }
        #endregion
        #endregion
        [RelayCommand]
        internal void Save()
        {
            foreach (var groupData in AllGroupDatas)
                groupData.Save();
            IsRemindSave = false;
        }

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
