using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HKW.Libs.Log4Cs;
using HKW.ViewModels.Controls;
using HKW.ViewModels.Dialogs;
using StarsectorTools.Libs.GameInfo;
using StarsectorTools.Libs.Utils;
using StarsectorToolsExtension.PortraitsManager.Models;

namespace StarsectorToolsExtension.PortraitsManager.ViewModels
{
    internal partial class PortraitsManagerViewModel
    {
        /// <summary>
        /// 单例化
        /// </summary>
        public static PortraitsManagerViewModel Instance { get; private set; } = null!;

        public const string _StrVanilla = "Vanilla";

        public void Close()
        {
            foreach (var groupData in AllGroupDatas)
                groupData.Close();
            AddFactionWindowViewModel?.Close();
        }

        public void DropPortraitFiles(Array array, Gender gender)
        {
            if (_nowSelectedFactionItem is null)
            {
                MessageBoxVM.Show(new("你必须选择一个势力"));
                return;
            }
            _nowGroupData.TryAddPortrait(
                array.OfType<string>(),
                _nowSelectedFactionItem.Id!,
                gender
            );
            OnNowShowMalePortraitItemsChanged(null!);
            OnNowShowFemalePortraitItemsChanged(null!);
        }

        private void ComboBox_GroupList_SelectionChangedEvent(ComboBoxItemVM item)
        {
            ChangeAllGroupData(item.Tag!.ToString()!);
            CleanShowPortraitItems();
        }

        private void CleanShowPortraitItems()
        {
            NowShowMalePortraitItems = null!;
            NowShowFemalePortraitItems = null!;
            RefreshMaleGroupBoxHeader();
            RefreshFemaleGroupBoxHeader();
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

        private void CleanFactionFilter()
        {
            foreach (var groupData in AllGroupDatas)
            {
                if (_originalFactionItemsSource.TryGetValue(groupData.GroupId, out var itemsSource))
                    groupData.FactionList.ItemsSource = itemsSource;
                groupData.IsEnabled = true;
            }
            _originalFactionItemsSource.Clear();
        }

        private void FactionFilter(string text)
        {
            foreach (var groupData in AllGroupDatas)
            {
                ObservableCollection<ListBoxItemVM> tempFactionList = new();
                // 使用已保存的原始分组
                if (
                    !_originalFactionItemsSource.TryGetValue(groupData.GroupId, out var itemsSource)
                )
                    itemsSource = groupData.FactionList.ItemsSource;
                foreach (var item in itemsSource)
                {
                    // 搜索I18n名称和ID
                    if (
                        item.Id!.Contains(text, StringComparison.OrdinalIgnoreCase)
                        || item.Name!.Contains(text, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        tempFactionList.Add(item);
                    }
                }
                if (tempFactionList.Any())
                {
                    // 如果过滤列表中含有项目,则保存原始列表并替换显示列表
                    _originalFactionItemsSource.TryAdd(
                        groupData.GroupId,
                        groupData.FactionList.ItemsSource
                    );
                    groupData.FactionList.ItemsSource = tempFactionList;
                    // 如果已选中的势力未包含在过滤后的列表中,清除选中项
                    if (!tempFactionList.Contains(_nowSelectedFactionItem))
                        CleanShowPortraitItems();
                }
                else
                {
                    // 如果过滤列表没有项目,则禁用分组点击并取消展开
                    groupData.IsEnabled = false;
                    groupData.IsExpanded = false;
                }
            }
        }

        #region ChangeAllGroupData
        private void ChangeAllGroupData(string groupTypeName)
        {
            FactionFilterText = string.Empty;
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
    }
}
