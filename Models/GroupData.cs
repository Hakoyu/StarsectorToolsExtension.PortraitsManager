using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using HKW.Libs.Log4Cs;
using HKW.ViewModels.Controls;
using HKW.ViewModels.Dialogs;
using StarsectorTools.Libs.GameInfo;
using StarsectorTools.Libs.Utils;

namespace StarsectorToolsExtension.PortraitsManager.Models
{
    internal partial class GroupData : ObservableObject
    {
        /// <summary>组Id</summary>
        public string GroupId { get; private set; }

        /// <summary>根目录</summary>
        public string BaseDirectory { get; private set; }

        /// <summary>势力目录</summary>
        public string FactionDirectory { get; private set; }

        /// <summary>拓展根目录</summary>
        public string PMDirectory { get; private set; }

        /// <summary>肖像目录</summary>
        public string PMPortraitsDirectory { get; private set; }

        /// <summary>备份目录</summary>
        public string PMBackupDirectory { get; private set; }

        /// <summary>势力备份目录</summary>
        public string PMFactionBackupDirectory { get; private set; }

        [ObservableProperty]
        private string _header = string.Empty;

        [ObservableProperty]
        private string _toolTip = string.Empty;

        [ObservableProperty]
        private ListBoxVM _factionList = new();

        [ObservableProperty]
        private Dictionary<string, FactionPortraits> _allFactionPortraits = new();

        [ObservableProperty]
        private Dictionary<string, ObservableCollection<ListBoxItemVM>> _maleFactionPortraitsItem =
            new();

        [ObservableProperty]
        private Dictionary<
            string,
            ObservableCollection<ListBoxItemVM>
        > _femaleFactionPortraitsItem = new();

        private Dictionary<string, Stream> _allImageStream = new();

        public GroupData(string groupId, string groupName, string baseDirectory)
        {
            Header = ToolTip = groupName;
            GroupId = groupId;
            BaseDirectory = baseDirectory;
            FactionDirectory = $"{baseDirectory}\\data\\world\\factions";
            PMDirectory = $"{baseDirectory}\\{nameof(StarsectorToolsExtension)}.PortraitsManager";
            PMPortraitsDirectory = $"{PMDirectory}\\Portraits";
            PMBackupDirectory = $"{PMDirectory}\\Backup";
            PMFactionBackupDirectory = $"{PMBackupDirectory}\\Faction";
            GetFactions(BaseDirectory, FactionDirectory);
        }

        private void GetFactions(string baseDirectory, string factionDirectory)
        {
            if (Utils.GetAllSubFiles(factionDirectory) is not List<FileInfo> fileList)
                return;
            foreach (var file in fileList)
            {
                try
                {
                    if (FactionPortraits.Create(file.FullName, baseDirectory, out var errMessage) is not FactionPortraits factionPortraits)
                        continue;
                    var faction = Path.GetFileNameWithoutExtension(file.FullName);
                    FactionList.Add(CreateFactionItem(faction, file.FullName));
                    var maleCollection = new ObservableCollection<ListBoxItemVM>();
                    var femaleCollection = new ObservableCollection<ListBoxItemVM>();
                    foreach (var portraitPath in factionPortraits.AllPortraitsPath)
                    {
                        var portraits = Path.GetFileNameWithoutExtension(portraitPath);
                        _allImageStream.TryAdd(
                            portraitPath,
                            new StreamReader(Path.Combine(factionPortraits.BaseDirectory, portraitPath)).BaseStream
                        );
                        if (factionPortraits.MalePortraitsPath.Contains(portraitPath))
                            maleCollection.Add(
                                CreatePortraitItem(
                                    portraits,
                                    portraitPath,
                                    _allImageStream[portraitPath]
                                )
                            );
                        if (factionPortraits.FemalePortraitsPath.Contains(portraitPath))
                            femaleCollection.Add(
                                CreatePortraitItem(
                                    portraits,
                                    portraitPath,
                                    _allImageStream[portraitPath]
                                )
                            );
                    }
                    MaleFactionPortraitsItem.Add(faction, maleCollection);
                    FemaleFactionPortraitsItem.Add(faction, femaleCollection);
                }
                catch (Exception ex)
                {
                    Logger.Error("???", ex);
                    MessageBoxVM.Show(new(ex.ToString()) { Icon = MessageBoxVM.Icon.Error });
                }
            }
        }

        private ListBoxItemVM CreateFactionItem(string faction, string factionPath)
        {
            return new()
            {
                Name = faction,
                Content = GetVanillaFactionI18n(faction),
                ToolTip = factionPath,
                Tag = this,
            };
        }

        private ListBoxItemVM CreatePortraitItem(
            string portrait,
            string portraitPath,
            Stream imageStream
        )
        {
            return new()
            {
                Name = portrait,
                ToolTip = portraitPath,
                Tag = imageStream,
            };
        }

        public void Close()
        {
            foreach (var kv in _allImageStream)
                kv.Value.Close();
        }
        private string GetVanillaFactionI18n(string factionId) =>
            VanillaFactions.AllVanillaFactionsI18n.TryGetValue(factionId, out var name)
                ? name
                : factionId;
    }
}
