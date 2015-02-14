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

        private const string LINK_HELP = "https://github.com/TilmanGriesel/DeeplyLost/wiki/Getting-Started";
        private const string LINK_ABOUT = "https://github.com/TilmanGriesel/DeeplyLost/wiki/About";
        private const string LINK_PUBLISHER = "http://beamteamgames.com/stranded-deep/";

        //--------------------------------------------------------------------------
        //
        //  Variables
        //
        //--------------------------------------------------------------------------

        private string _saveGamePath;
        private FileSystemWatcher _fileWatcher;
        private bool _fileUpdatedDialogOpen = false;

        //--------------------------------------------------------------------------
        //
        //  Init
        //
        //--------------------------------------------------------------------------

        public MainForm()
        {
            InitializeComponent();

            this.flowLayoutPanel.Hide();

            Assembly assembly = Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            string version = fvi.FileVersion;
            this.Text += " (" + version + ")";

            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(Form1_DragEnter);
            this.DragDrop += new DragEventHandler(Form1_DragDrop);
        }

        //--------------------------------------------------------------------------
        //
        // Eventhandler
        //
        //--------------------------------------------------------------------------

        void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 1)
            {
                _saveGamePath = files[0];
                string fileName = Path.GetFileName(_saveGamePath);
                if (fileName == SAVE_FILE_NAME)
                {
                    _fileWatcher = new FileSystemWatcher();
                    _fileWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size;
                    _fileWatcher.Path = Path.GetDirectoryName(_saveGamePath);
                    _fileWatcher.Changed += new FileSystemEventHandler(OnChanged);
                    _fileWatcher.EnableRaisingEvents = true;
                    
                    // Get valid home nodes
                    bool nodesFounds = updateSaveGame();
                    if(nodesFounds)
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
            else
            {
                MessageBox.Show("Please drop only the \"" + SAVE_FILE_NAME + "\" into the window.",
                    "Multiple files detected!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            // Specify what is done when a file is changed, created, or deleted.
            Console.WriteLine("File: " + e.FullPath + " " + e.ChangeType);
            
            if(e.ChangeType == WatcherChangeTypes.Changed)
            {
                _fileUpdatedDialogOpen = true;
                _fileWatcher.EnableRaisingEvents = false;
                DialogResult dialogResult = MessageBox.Show("The save game was updated, rescan for new islands?",
                                        "Save game was updated externally", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (dialogResult == DialogResult.Yes)
                {
                    this.BeginInvoke(new MethodInvoker(() => updateSaveGame()));
                }
                _fileWatcher.EnableRaisingEvents = true;
                _fileUpdatedDialogOpen = false;
            }
        }

        //--------------------------------------------------------------------------
        //
        //  Private methods
        //
        //--------------------------------------------------------------------------

        private bool updateSaveGame()
        {
            List<HomeNode> homeNodes = gatherHomeNodes();
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

        private void createUIElements(List<HomeNode> homeNodes)
        {
            flowLayoutPanel.Controls.Clear();
            foreach(HomeNode node in homeNodes)
            {
                flowLayoutPanel.Controls.Add(createHomeNodeUi(node));
            }
        }

        private Panel createHomeNodeUi(HomeNode homeNode)
        {
            string displayName = homeNode.Alias != "none" ? homeNode.Alias : homeNode.Name;

            Panel itemPanel = new Panel();
            itemPanel.Size = new Size(100, 100);
            itemPanel.BackColor = Color.Gainsboro;

            Label itemLabel = new Label();
            itemLabel.Size = new Size(80, 35);
            itemLabel.Location = new Point(10, 10);
            itemLabel.AutoEllipsis = true;
            itemLabel.Text = displayName;
            itemLabel.MouseDoubleClick += new MouseEventHandler(delegate(Object o, MouseEventArgs a)
            {
                using(RenameHomeNode form = new RenameHomeNode())
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        saveNodeAlias(homeNode.Name, form.NodeName);
                        itemLabel.Text = form.NodeName;
                    }
                }
            });

            Button teleportButton = new Button();
            teleportButton.Size = new Size(80, 40);
            teleportButton.Location = new Point(10, 45);
            teleportButton.Text = "Teleport";
            teleportButton.BackColor = SystemColors.Control;

            itemPanel.Controls.Add(teleportButton);
            itemPanel.Controls.Add(itemLabel);

            teleportButton.MouseClick += new MouseEventHandler(delegate(Object o, MouseEventArgs a)
            {
                DialogResult dialogResult = MessageBox.Show("Are you sure that you want to teleport to  \"" + displayName + "\"?",
                                                            "Preparing Teleport", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (dialogResult == DialogResult.Yes)
                {
                    teleportToHomeNode(homeNode);
                }
                else if (dialogResult == DialogResult.No)
                {
                    Console.WriteLine("Teleport to {0} aborted!", homeNode.Name);
                }
            });

            return itemPanel;
        }

        private List<HomeNode> gatherHomeNodes()
        {
            List<HomeNode> homeNodes = new List<HomeNode>();

            try
            {
                if (File.Exists(_saveGamePath))
                {
                    string sgJson = File.ReadAllText(_saveGamePath);
                    dynamic sgData = JsonConvert.DeserializeObject<JObject>(sgJson);
                    foreach (dynamic node in sgData.Persistent.TerrainGeneration.Nodes)
                    {
                        HomeNode homeNode;
                        dynamic cnode = node.First;
                        if (cnode.biome == Biomes.ISLAND)
                        {
                            if (cnode.Objects != null)
                            {
                                foreach (dynamic obj in cnode.Objects)
                                {
                                    dynamic cobj = obj.First;
                                    if (cobj.name == GameObjects.FIRE)
                                    {
                                        homeNode = new HomeNode();
                                        // Collect node data 
                                        homeNode.Name = cnode.name;
                                        homeNode.OriginX = cnode.positionOffset.x;
                                        homeNode.OriginZ = cnode.positionOffset.z;
                                        homeNode.LocalX = cobj.Transform.localPosition.x;
                                        homeNode.LocalY = cobj.Transform.localPosition.y;
                                        homeNode.LocalZ = cobj.Transform.localPosition.z;
                                        // Remove unnecessary data from node name
                                        homeNode.Name = homeNode.Name.Substring(homeNode.Name.IndexOf(Biomes.ISLAND));
                                        // Get alias if available
                                        homeNode.Alias = getNodeAlias(homeNode.Name);
                                        // Add home node to collected nodes
                                        homeNodes.Add(homeNode);
                                    }
                                }
                            }
                        }
                    }
                    return homeNodes;
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

        private void teleportToHomeNode(HomeNode homeNode)
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

                    sgData.Persistent.TerrainGeneration.WorldOriginPoint.x = homeNode.OriginX;
                    sgData.Persistent.TerrainGeneration.WorldOriginPoint.z = homeNode.OriginZ;

                    sgData.Persistent.TerrainGeneration.playerPosition.x = homeNode.LocalX;
                    sgData.Persistent.TerrainGeneration.playerPosition.y = homeNode.LocalY;
                    sgData.Persistent.TerrainGeneration.playerPosition.z = homeNode.LocalZ;

                    sgData.Persistent.PlayerMovement.Transform.localPosition.x = homeNode.LocalX;
                    sgData.Persistent.PlayerMovement.Transform.localPosition.y = homeNode.LocalY;
                    sgData.Persistent.PlayerMovement.Transform.localPosition.z = homeNode.LocalZ;

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
    }

    class HomeNode
    {
        public string Name;
        public string Alias;
        public string OriginX;
        public string OriginZ;
        public string LocalX;
        public string LocalY;
        public string LocalZ;
    }
}
