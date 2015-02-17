/*
 * The MIT License (MIT)
 * Copyright (c) <year> <copyright holders>
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 * 
 * 2015 - Tilman Griesel <https://github.com/TilmanGriesel/> <http://rocketengine.io/>
 */

using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeeplyLost
{
    public partial class MainForm : Form
    {
        //--------------------------------------------------------------------------
        //
        //  Constats
        //
        //--------------------------------------------------------------------------

        private const string REG_KEY = "Software\\DeeplyLost";
        private const string SAVE_FILE_NAME = "Save.json";

        private const string LINK_UPDATE = "http://tilmangriesel.github.io/DeeplyLost/update/update.json";
        private const string LINK_HELP = "http://tilmangriesel.github.io/DeeplyLost/";
        private const string LINK_ABOUT = "http://tilmangriesel.github.io/DeeplyLost/";
        private const string LINK_PUBLISHER = "http://beamteamgames.com/stranded-deep/";

        private enum IslandQueryMode { Fire, All };

        //--------------------------------------------------------------------------
        //
        //  Variables
        //
        //--------------------------------------------------------------------------

        private string _version;
        private string _saveGamePath;
        private float _spawnOffset = 0;

        private IslandQueryMode _islandQueryMode = IslandQueryMode.Fire;
        private FileSystemWatcher _fileWatcher;

        //--------------------------------------------------------------------------
        //
        //  Init
        //
        //--------------------------------------------------------------------------

        public MainForm()
        {
            InitializeComponent();
            getAssemblyVersion();
            checkForUpdate();
            getCommandLineSettings();

            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(MainForm_DragEnter);
            this.DragDrop += new DragEventHandler(MainForm_DragDrop);

            this.flowLayoutPanel.Hide();
            this.Text += " (" + _version + ")";

            _saveGamePath = getSGFilePath();
            if (_saveGamePath != "none")
            {
                loadSaveGameFromPath(_saveGamePath, false);
            }
        }

        //--------------------------------------------------------------------------
        //
        // Eventhandler
        //
        //--------------------------------------------------------------------------

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 1)
            {
                _saveGamePath = files[0];
                loadSaveGameFromPath(_saveGamePath, true);
            }
            else
            {
                MessageBox.Show("Please drop only the \"" + SAVE_FILE_NAME + "\" into the window.",
                    "Multiple files detected!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            if(e.ChangeType == WatcherChangeTypes.Changed)
            {
                _fileWatcher.EnableRaisingEvents = false;
                DialogResult dialogResult = MessageBox.Show("The save game was updated, rescan for new islands?",
                                        "Save game was updated externally", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (dialogResult == DialogResult.Yes)
                {
                    this.BeginInvoke(new MethodInvoker(() => getSaveGameNodes()));
                }
                _fileWatcher.EnableRaisingEvents = true;
            }
        }

        //--------------------------------------------------------------------------
        //
        //  Private methods
        //
        //--------------------------------------------------------------------------

        //--------------------------------------------------------------------------
        //  Generic
        //--------------------------------------------------------------------------

        private void getAssemblyVersion()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            _version = fvi.FileVersion;
        }

        private void checkForUpdate()
        {
            WebClient client = new WebClient();
            try
            {
                string updateJson = client.DownloadString(LINK_UPDATE);
                UpdateInfo updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(updateJson);
                if(updateInfo.version != _version)
                {
                    DialogResult dialogResult = MessageBox.Show("A newer version of DeeplyLost is available. Do you want to download it?", "New Version Found", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (dialogResult == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start(updateInfo.url);
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Failed to get update information. Message: {0}", ex.Message);
            }
        }

        private void getCommandLineSettings()
        {
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            for (int i = 0; i < commandLineArgs.Length; i++)
            {
                string curr = commandLineArgs[i];
                string next = null;

                if (i + 1 < commandLineArgs.Length)
                {
                    next = commandLineArgs[i + 1];
                }

                if (curr == "-mode")
                {
                    if (next == "all")
                    {
                        _islandQueryMode = IslandQueryMode.All;
                        MessageBox.Show("Island query mode is set to \"all\".\n" +
                            "Is is now possible to visit islands without a campfire on it.", "Query mode changed");
                    }
                }
                else if(curr == "-offset")
                {
                    _spawnOffset = float.Parse(next);
                    MessageBox.Show("Changed spawn offset to: \"" + _spawnOffset + "\".", "Spawn offset changed");
                }
            }
        }

        //--------------------------------------------------------------------------
        //  Save game parsing
        //--------------------------------------------------------------------------

        private void loadSaveGameFromPath(string saveGamePath, bool calledByUser)
        {
            string fileName = Path.GetFileName(_saveGamePath);
            if (fileName == SAVE_FILE_NAME)
            {
                // Ask the user if he/she wants to keep the
                // path stored
                if(calledByUser)
                {
                    DialogResult dialogResult = MessageBox.Show("Keep save game path for next use?", "Keep save game path?", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (dialogResult == DialogResult.Yes)
                    {
                        saveSGFilePath(_saveGamePath);
                    }
                }

                // Dropped file seems valid
                this.FormBorderStyle = FormBorderStyle.Sizable;

                _fileWatcher = new FileSystemWatcher();
                _fileWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size;
                _fileWatcher.Path = Path.GetDirectoryName(_saveGamePath);
                _fileWatcher.Changed += new FileSystemEventHandler(OnChanged);
                _fileWatcher.EnableRaisingEvents = true;

                // Get valid home nodes
                bool nodesFounds = getSaveGameNodes();
                if (nodesFounds)
                {
                    this.panelIntro.Hide();
                    this.flowLayoutPanel.Show();
                }
                else
                {
                    MessageBox.Show("No islands with a campfire found!\nPlease place a campfire on a island to enable it for teleportation.\n" +
                        "If the problem persists it is possible that this version of DeeplyLost is not compatible with your StrandedDeep version.",
                        "No home base found!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                MessageBox.Show("It seems that the file you dropped is not a valid save game.\nEnsure that you drop the file \"" + SAVE_FILE_NAME + "\"",
                    "Invalid save game!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool getSaveGameNodes()
        {
            List<IslandNode> homeNodes = gatherIslandNodes();
            if (homeNodes != null && homeNodes.Count > 0)
            {
                createUIElements(homeNodes);
                return true;
            }
            else
            {
                return false;
            }
        }

        private List<IslandNode> gatherIslandNodes()
        {
            List<IslandNode> islandNodes = new List<IslandNode>();
            try
            {
                if (File.Exists(_saveGamePath))
                {
                    string sgJson = File.ReadAllText(_saveGamePath);
                    dynamic sgData = JsonConvert.DeserializeObject<JObject>(sgJson);
                    foreach (dynamic node in sgData.Persistent.TerrainGeneration.Nodes)
                    {
                        IslandNode islandNode;
                        dynamic cnode = node.First;
                        if (cnode.biome == Biomes.ISLAND)
                        {
                            if (cnode.fullyGenerated == true)
                            {
                                if (cnode.Objects != null)
                                {
                                    islandNode = new IslandNode();
                                    // Collect node data 
                                    islandNode.Name = cnode.name;
                                    islandNode.OriginX = getFloatFromGameFloat((string)cnode.positionOffset.x);
                                    islandNode.OriginZ = getFloatFromGameFloat((string)cnode.positionOffset.z);
                                    // Remove unnecessary data from node name
                                    islandNode.Name = islandNode.Name.Substring(islandNode.Name.IndexOf(Biomes.ISLAND));
                                    // Get alias if available
                                    islandNode.Alias = getNodeAlias(islandNode.Name);

                                    // Get home identifier
                                    string homeIdent = null;
                                    if (_islandQueryMode == IslandQueryMode.Fire) homeIdent = GameObjects.FIRE;
                                    else if (_islandQueryMode == IslandQueryMode.All) homeIdent = GameObjects.CRAB_HOME;

                                    // Store last crab home
                                    // Crab homes are used as a pre-spawn location
                                    float refX = 0;
                                    float refY = 0;
                                    float refZ = 0;

                                    foreach (dynamic obj in cnode.Objects)
                                    {
                                        dynamic cobj = obj.First;                                     

                                        if (cobj.name == GameObjects.CRAB_HOME)
                                        {
                                            refX = getFloatFromGameFloat((string)cobj.Transform.localPosition.x);
                                            refY = getFloatFromGameFloat((string)cobj.Transform.localPosition.y);
                                            refZ = getFloatFromGameFloat((string)cobj.Transform.localPosition.z);
                                        }
                                        
                                        if (cobj.name == homeIdent)
                                        {
                                            islandNode.IsHome = true;
                                        }

                                        // Gather item name and amount later usage.
                                        string readableName = getReadableNameFromGameObject((string)cobj.name);
                                        if (!islandNode.Items.ContainsKey(readableName))
                                        {
                                            islandNode.Items[readableName] = 1;
                                        }
                                        else
                                        {
                                            islandNode.Items[readableName] += 1;
                                        }
                                    }

                                    // Currently we only add home nodes,
                                    // this is in subject to change.
                                    if (islandNode.IsHome)
                                    {
                                        // Assign reference point as pre-spwan position
                                        islandNode.LocalX = refX;
                                        islandNode.LocalY = refY;
                                        islandNode.LocalZ = refZ;

                                        // Apply spawn offset to prevent
                                        // pre-spawn problems
                                        islandNode.LocalX += _spawnOffset;
                                        islandNode.LocalY += _spawnOffset;
                                        islandNode.LocalZ += _spawnOffset;

                                        // Add home node to collected nodes
                                        islandNodes.Add(islandNode);
                                    }
                                }
                            }
                        }
                    }
                    return islandNodes;
                }
                else
                {
                    Console.WriteLine("Cannot find save game.");
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine("Unable to get save game. {0}", ex.Message);
            }
            return null;
        }

        //--------------------------------------------------------------------------
        //  Teleportation
        //--------------------------------------------------------------------------

        private void teleportToHomeNode(IslandNode islandNode)
        {
            if (File.Exists(_saveGamePath))
            {
                _fileWatcher.EnableRaisingEvents = false;
                Int32 unixTimestamp = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                string backupPath = _saveGamePath + ".bak_" + unixTimestamp;

                // Create backup
                try
                {
                    File.Copy(_saveGamePath, backupPath);
                }
                catch(Exception ex)
                {
                    MessageBox.Show("Failed to create save game backup. Aborting teleportation!\nMessage:" + ex.Message,
                        "Teleportation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    string sgJson = File.ReadAllText(_saveGamePath);
                    dynamic sgData = JsonConvert.DeserializeObject<JObject>(sgJson);

                    sgData.Persistent.TerrainGeneration.WorldOriginPoint.x = getGameFloatFromFloat(islandNode.OriginX);
                    sgData.Persistent.TerrainGeneration.WorldOriginPoint.z = getGameFloatFromFloat(islandNode.OriginZ);

                    sgData.Persistent.TerrainGeneration.playerPosition.x = getGameFloatFromFloat(islandNode.LocalX);
                    sgData.Persistent.TerrainGeneration.playerPosition.y = getGameFloatFromFloat(islandNode.LocalY);
                    sgData.Persistent.TerrainGeneration.playerPosition.z = getGameFloatFromFloat(islandNode.LocalZ);

                    sgData.Persistent.PlayerMovement.Transform.localPosition.x = getGameFloatFromFloat(islandNode.LocalX);
                    sgData.Persistent.PlayerMovement.Transform.localPosition.y = getGameFloatFromFloat(islandNode.LocalY);
                    sgData.Persistent.PlayerMovement.Transform.localPosition.z = getGameFloatFromFloat(islandNode.LocalZ);

                    sgJson = JsonConvert.SerializeObject(sgData);
                    File.WriteAllText(_saveGamePath, sgJson);
                    _fileWatcher.EnableRaisingEvents = true;

                    MessageBox.Show("Please \"quit\" your game if needed and press \"Load Game\" to get teleported " +
                        "to the selected island. You will spawn next to a campfire at the island.\n\n" +
                        "A backup of the last save game is stored at:  \"" + backupPath +  "\"",
                        "Teleportation completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch(Exception ex)
                {
                    MessageBox.Show("Failed to update save game!\nMessage:" + ex.Message,
                        "Teleportation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        //--------------------------------------------------------------------------
        //  Dynamic UI
        //--------------------------------------------------------------------------

        private void createUIElements(List<IslandNode> islandNodes)
        {
            flowLayoutPanel.Controls.Clear();
            foreach (IslandNode node in islandNodes)
            {
                flowLayoutPanel.Controls.Add(createIslandNodeUi(node));
            }
        }

        private Panel createIslandNodeUi(IslandNode islandNode)
        {
            string displayName = islandNode.Alias != "none" ? islandNode.Alias : islandNode.Name;

            // Different colors maybe used in a later version
            string panelColorDefault = "#dcdcdc";
            string panelColorHome = "#dcdcdc";

            // Create the main panel
            Panel itemPanel = new Panel();
            itemPanel.Size = new Size(110, 110);
            itemPanel.BackColor = System.Drawing.ColorTranslator.FromHtml(islandNode.IsHome ? panelColorHome : panelColorDefault);

            // Create island label
            Label itemLabel = new Label();
            itemLabel.Size = new Size(80, 35);
            itemLabel.Location = new Point(10, 10);
            itemLabel.AutoEllipsis = true;
            itemLabel.Text = displayName;
            itemPanel.Controls.Add(itemLabel);
            itemLabel.MouseDoubleClick += new MouseEventHandler(delegate(Object o, MouseEventArgs a)
            {
                using (RenameIslandNode form = new RenameIslandNode())
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        saveNodeAlias(islandNode.Name, form.NodeName);
                        displayName = itemLabel.Text = islandNode.Alias = form.NodeName;
                    }
                }
            });

            // Create teleport button
            if(islandNode.IsHome)
            {
                Button teleportButton = new Button();
                teleportButton.Size = new Size(90, 40);
                teleportButton.Location = new Point(10, 45);
                teleportButton.Text = "Teleport";
                teleportButton.BackColor = SystemColors.Control;
                itemPanel.Controls.Add(teleportButton);

                teleportButton.MouseClick += new MouseEventHandler(delegate(Object o, MouseEventArgs a)
                {
                    DialogResult dialogResult = MessageBox.Show("Are you sure that you want to teleport to  \"" + displayName + "\"?",
                                                                "Preparing Teleport", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (dialogResult == DialogResult.Yes)
                    {
                        teleportToHomeNode(islandNode);
                    }
                    else if (dialogResult == DialogResult.No)
                    {
                        Console.WriteLine("Teleport to {0} aborted!", islandNode.Name);
                    }
                });
            }

            // Create island info popup button
            Button itemButton = new Button();
            itemButton.Size = new Size(20, 20);
            itemButton.Location = new Point(90, 90);
            itemButton.Text = "?";
            itemButton.BackColor = SystemColors.Control;
            itemPanel.Controls.Add(itemButton);
            itemButton.MouseClick += new MouseEventHandler(delegate(Object o, MouseEventArgs a)
            {
                using (IslandItems form = new IslandItems())
                {
                    form.SetTitle(displayName);
                    form.AddGridData(islandNode.Items);
                    form.ShowDialog();
                }
            });

            return itemPanel;
        }

        //--------------------------------------------------------------------------
        //  Conversion helper
        //--------------------------------------------------------------------------

        private float getFloatFromGameFloat(string gameFloat)
        {
            return float.Parse(gameFloat.Substring(2));
        }

        private string getGameFloatFromFloat(float stdFloat)
        {
            return "~f" + stdFloat.ToString();
        }

        private string getReadableNameFromGameObject(string gameObjectName)
        {
            // This can be optimized
            gameObjectName = gameObjectName.Split(new string[] { "(Clone)" }, StringSplitOptions.None)[0];
            gameObjectName = gameObjectName.Replace("_", " ");
            return gameObjectName;
        }

        //--------------------------------------------------------------------------
        //  Reg helper
        //--------------------------------------------------------------------------

        private void saveNodeAlias(string nodeName, string alias)
        {
            RegistryKey key;
            key = Registry.CurrentUser.CreateSubKey(REG_KEY);
            key.SetValue(nodeName, alias);
            key.Close();
        }

        private string getNodeAlias(string nodeName)
        {
            RegistryKey key;
            key = Registry.CurrentUser.CreateSubKey(REG_KEY);
            string retval = key.GetValue(nodeName, "none").ToString();
            key.Close();
            return retval;
        }

        private void saveSGFilePath(string path)
        {
            RegistryKey key;
            key = Registry.CurrentUser.CreateSubKey(REG_KEY);
            key.SetValue("savegame", path);
            key.Close();
        }

        private string getSGFilePath()
        {
            RegistryKey key;
            key = Registry.CurrentUser.CreateSubKey(REG_KEY);
            string retval = key.GetValue("savegame", "none").ToString();
            key.Close();
            return retval;
        }

        //--------------------------------------------------------------------------
        //  External page helper
        //--------------------------------------------------------------------------

        private void linkLabelHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(LINK_HELP);
        }

        private void linkLabelAbout_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(LINK_ABOUT);
        }

        private void linkPublisher_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(LINK_PUBLISHER);
        }
    }

    class Biomes
    {
        public const string ISLAND = "ISLAND"; 
    }

    class GameObjects
    {
        public const string FIRE = "FIRE(Clone)";
        public const string CRAB_HOME = "CRAB_HOME(Clone)";
    }

    class IslandNode
    {
        public string Name;
        public bool IsHome;
        public string Alias;
        public float OriginX;
        public float OriginZ;
        public float LocalX;
        public float LocalY;
        public float LocalZ;
        public IDictionary<string, int> Items = new Dictionary<string, int>();
    }

    class UpdateInfo
    {
        public string version;
        public string url;
        public string branch;
    }

}
