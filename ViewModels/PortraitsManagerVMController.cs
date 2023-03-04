using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HKW.ViewModels.Dialogs;
using StarsectorToolsExtension.PortraitsManager.Models;

namespace StarsectorToolsExtension.PortraitsManager.ViewModels
{
    internal partial class PortraitsManagerViewModel
    {

        /// <summary>
        /// 单例化
        /// </summary>
        public static PortraitsManagerViewModel Instance { get; private set; } = null!;

        public const string strVanilla = "Vanilla";

        public void Close()
        {
            foreach (var groupData in AllGroupDatas)
                groupData.Close();
        }

        public void DropPortraitFiles(Array array, Gender gender)
        {
            if (_nowSelectedFactionItem is null)
            {
                MessageBoxVM.Show(new("你必须选择一个势力"));
                return;
            }
            _nowGroupData.TryAddPortrait(array.OfType<string>(), _nowSelectedFactionItem.Name!, gender);
            IsRemindSave = true;
        }
    }
}
