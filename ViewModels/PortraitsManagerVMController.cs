using System;
using System.Collections.ObjectModel;
using System.Linq;
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
            VanillaGroupData.Close();
        }

        public void DropPortraitFiles(Array array, Gender gender)
        {
            if (_nowSelectedFactionItem is null)
            {
                MessageBoxVM.Show(new("你必须选择一个势力"));
                return;
            }
            VanillaGroupData.TryAddPortrait(
                array.OfType<string>(),
                _nowSelectedFactionItem.Id!,
                gender
            );
            OnNowShowMalePortraitItemsChanged(null!);
            OnNowShowFemalePortraitItemsChanged(null!);
        }

        public void CleanShowPortraitItems()
        {
            NowShowMalePortraitItems = null!;
            NowShowFemalePortraitItems = null!;
            RefreshMaleGroupBoxHeader();
            RefreshFemaleGroupBoxHeader();
        }

        #region ChangeAllGroupData

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
            VanillaGroupData = groupData;
            MalePortraitsFilterText = string.Empty;
            FemalePortraitsFilterText = string.Empty;
            NowShowMalePortraitItems = groupData.MaleFactionPortraitItems[
                _nowSelectedFactionItem.Id!
            ];
            NowShowFemalePortraitItems = groupData.FemaleFactionPortraitItems[
                _nowSelectedFactionItem.Id!
            ];
            NowShowMalePortraitItems.CollectionChanged += (s, e) =>
                MalePortraitFilter(MalePortraitsFilterText);
            NowShowFemalePortraitItems.CollectionChanged += (s, e) =>
                FemalePortraitFilter(FemalePortraitsFilterText);
            Logger.Info($"切换至 分组: {VanillaGroupData.Header} 势力: {_nowSelectedFactionItem.Name}");
        }

        private void MalePortraitFilter(string filterText)
        {
            if (VanillaGroupData is null)
                return;
            if (string.IsNullOrWhiteSpace(filterText))
            {
                NowShowMalePortraitItems = VanillaGroupData.MaleFactionPortraitItems[
                    _nowSelectedFactionItem.Id!
                ];
            }
            else
            {
                NowShowMalePortraitItems = new(
                    VanillaGroupData.MaleFactionPortraitItems[_nowSelectedFactionItem.Id!].Where(
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
            if (VanillaGroupData is null)
                return;
            if (string.IsNullOrWhiteSpace(filterText))
            {
                NowShowFemalePortraitItems = VanillaGroupData.FemaleFactionPortraitItems[
                    _nowSelectedFactionItem.Id!
                ];
            }
            else
            {
                NowShowFemalePortraitItems = new(
                    VanillaGroupData.FemaleFactionPortraitItems[_nowSelectedFactionItem.Id!].Where(
                        i =>
                            i.Content!
                                .ToString()!
                                .Contains(filterText, StringComparison.OrdinalIgnoreCase)
                    )
                );
                Logger.Info($"女性肖像列表搜索: {filterText}");
            }
        }

        #endregion PortraitFilter

        #endregion ChangeAllGroupData
    }
}