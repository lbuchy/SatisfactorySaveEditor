using System;
using GalaSoft.MvvmLight;
using SatisfactorySaveEditor.Model;
using SatisfactorySaveParser;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using GalaSoft.MvvmLight.CommandWpf;
using SatisfactorySaveEditor.Util;
using System.Windows;
using Microsoft.Win32;
using System.Reflection;
using SatisfactorySaveEditor.View;
using SatisfactorySaveParser.PropertyTypes;
using System.IO;
using System.Linq;
using GongSolutions.Wpf.DragDrop;
using SatisfactorySaveEditor.ViewModel.Property;
using SatisfactorySaveParser.Data;
using SatisfactorySaveEditor.Cheats;

namespace SatisfactorySaveEditor.ViewModel
{
    public class MainViewModel : ViewModelBase, IDropTarget
    {
        private SatisfactorySave saveGame;
        private SaveObjectModel rootItem;
        private SaveObjectModel selectedItem;

        public ObservableCollection<SaveObjectModel> RootItem => new ObservableCollection<SaveObjectModel> { rootItem };

        public SaveObjectModel SelectedItem
        {
            get => selectedItem;
            set { Set(() => SelectedItem, ref selectedItem, value); }
        }

        public string FileName
        {
            get
            {
                if (saveGame == null) return string.Empty;
                return string.Format(" - {1} [{0}]", saveGame.FileName, saveGame.Header.SessionName);
            }
        }

        public ObservableCollection<string> LastFiles { get; } = new ObservableCollection<string>();

        public ObservableCollection<ICheat> CheatMenuItems { get; } = new ObservableCollection<ICheat>();

        public RelayCommand<SaveObjectModel> TreeSelectCommand { get; }
        public RelayCommand<string> JumpCommand { get; }
        public RelayCommand ExitCommand { get; }
        public RelayCommand<string> OpenCommand { get; }
        public RelayCommand AboutCommand { get; }
        public RelayCommand<SaveObjectModel> DeleteCommand { get; }
        public RelayCommand<ICheat> CheatCommand { get; }
        public RelayCommand<bool> SaveCommand { get; }

        public bool HasUnsavedChanges { get; set; } //TODO: set this to true when any value in WPF is changed. current plan for this according to goz3rr is to make a wrapper for the data from the parser and then change the set method in the wrapper

        public MainViewModel()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && File.Exists(args[1])) LoadFile(args[1]);

            var savedFiles = Properties.Settings.Default.LastSaves?.Cast<string>().ToList();
            if (savedFiles == null) LastFiles = new ObservableCollection<string>();
            else LastFiles = new ObservableCollection<string>(savedFiles);

            // TODO: load this dynamically
            CheatMenuItems.Add(new ResearchUnlockCheat());
            CheatMenuItems.Add(new UnlockMapCheat());
            CheatMenuItems.Add(new InventorySlotsCheat());

            TreeSelectCommand = new RelayCommand<SaveObjectModel>(SelectNode);
            JumpCommand = new RelayCommand<string>(Jump, CanJump);
            ExitCommand = new RelayCommand(Exit);
            OpenCommand = new RelayCommand<string>(Open);
            AboutCommand = new RelayCommand(About);
            DeleteCommand = new RelayCommand<SaveObjectModel>(Delete, CanDelete);
            SaveCommand = new RelayCommand<bool>(Save, CanSave);
            CheatCommand = new RelayCommand<ICheat>(Cheat, CanCheat);
        }

        private bool CanDelete(SaveObjectModel model)
        {
            return model != rootItem;
        }

        private void Delete(SaveObjectModel model)
        {
            rootItem.Remove(model);
            RaisePropertyChanged(() => RootItem);
        }

        private bool CanCheat(ICheat cheat)
        {
            return rootItem != null;
        }

        private void Cheat(ICheat cheat)
        {
            if (cheat.Apply(rootItem))
                HasUnsavedChanges = true;
        }

        private bool CanSave(bool saveAs)
        {
            return saveGame != null;
        }

        private void Save(bool saveAs)
        {
            if (saveAs)
            {
                SaveFileDialog dialog = new SaveFileDialog
                {
                    Filter = "Satisfactory save file|*.sav",
                    InitialDirectory = Path.GetDirectoryName(saveGame.FileName),
                    DefaultExt = ".sav",
                    CheckFileExists = false,
                    AddExtension = true
                };

                if (dialog.ShowDialog() == true)
                {
                    saveGame.Entries.Clear();
                    saveGame.Entries.AddRange(rootItem.DescendantSelf);

                    rootItem.ApplyChanges();
                    saveGame.Save(dialog.FileName);
                    HasUnsavedChanges = false;
                    RaisePropertyChanged(() => FileName);
                }
            }
            else
            {
                saveGame.Entries.Clear();
                saveGame.Entries.AddRange(rootItem.DescendantSelf);

                rootItem.ApplyChanges();
                saveGame.Save();
                HasUnsavedChanges = false;
            }
        }

        private bool CanJump(string target)
        {
            return rootItem.FindChild(target, false) != null;
        }

        private void About()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            MessageBox.Show($"Satisfactory save editor{Environment.NewLine}{version}", "About");
        }

        private void Open(string fileName)
        {
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                LoadFile(fileName);
                HasUnsavedChanges = false;

                return;
            }

            if (HasUnsavedChanges)
            {
                MessageBoxResult result = MessageBox.Show("You have unsaved changes. Abandon changes by opening another file?\n\nNote: Changes made in the data text fields are not yet tracked as saved/unsaved but are still saved.", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            OpenFileDialog dialog = new OpenFileDialog
            {
                Filter = "Satisfactory save file|*.sav"
            };

            var newPath = Environment.ExpandEnvironmentVariables(@"%localappdata%\FactoryGame\Saved\SaveGames\");
            var oldPath = Environment.ExpandEnvironmentVariables(@"%userprofile%\Documents\My Games\FactoryGame\SaveGame\");

            if (Directory.Exists(newPath)) dialog.InitialDirectory = newPath;
            else dialog.InitialDirectory = oldPath;

            if (dialog.ShowDialog() == true)
            {
                LoadFile(dialog.FileName);
                HasUnsavedChanges = false;
            }
        }

        private void Exit()
        {
            if (HasUnsavedChanges)
            {
                MessageBoxResult result = MessageBox.Show("You have unsaved changes. Close and abandon changes?\n\nNote: Changes made in the data text fields are not yet tracked as saved/unsaved but are still saved.", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                    Application.Current.Shutdown();
                else
                    return;
            }
            else
            {
                Application.Current.Shutdown();
            }

        }

        private void Jump(string target)
        {
            SelectedItem.IsSelected = false;
            SelectedItem = rootItem.FindChild(target, true);
        }

        private void SelectNode(SaveObjectModel node)
        {
            SelectedItem = node;
        }

        private void LoadFile(string path)
        {
            SelectedItem = null;

            saveGame = new SatisfactorySave(path);

            rootItem = new SaveObjectModel("Root");
            var saveTree = new EditorTreeNode("Root");

            foreach (var entry in saveGame.Entries)
            {
                var parts = entry.TypePath.TrimStart('/').Split('/');
                saveTree.AddChild(parts, entry);
            }

            BuildNode(rootItem.Items, saveTree);

            rootItem.IsExpanded = true;
            foreach (var item in rootItem.Items)
            {
                item.IsExpanded = true;
            }

            RaisePropertyChanged(() => RootItem);
            RaisePropertyChanged(() => FileName);

            if (Properties.Settings.Default.LastSaves == null)
            {
                Properties.Settings.Default.LastSaves = new StringCollection();
            }

            if (LastFiles.Contains(path)) // No duplicates
            {
                Properties.Settings.Default.LastSaves.Remove(path);
                LastFiles.Remove(path);
            }

            Properties.Settings.Default.LastSaves.Add(path);
            LastFiles.Add(path);

            while (Properties.Settings.Default.LastSaves.Count >= 6) // Keeps only 5 most recent saves
            {
                LastFiles.RemoveAt(0);
                Properties.Settings.Default.LastSaves.RemoveAt(0);
            }

            Properties.Settings.Default.Save();
        }

        private void BuildNode(ObservableCollection<SaveObjectModel> items, EditorTreeNode node)
        {
            foreach (var child in node.Children)
            {
                var childItem = new SaveObjectModel(child.Value.Name);
                BuildNode(childItem.Items, child.Value);
                items.Add(childItem);
            }

            foreach (var entry in node.Content)
            {
                switch (entry)
                {
                    case SaveEntity se:
                        items.Add(new SaveEntityModel(se));
                        break;
                    case SaveComponent sc:
                        items.Add(new SaveComponentModel(sc));
                        break;
                }
            }
        }

        public void DragOver(IDropInfo dropInfo)
        {
            if (!(dropInfo.Data is DataObject data)) return;

            var files = data.GetFileDropList();
            if (files == null || files.Count == 0) return;

            dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            dropInfo.Effects = DragDropEffects.Copy;
        }

        public void Drop(IDropInfo dropInfo)
        {
            var fileName = ((DataObject)dropInfo.Data).GetFileDropList()[0];
            LoadFile(fileName);
        }
    }
}