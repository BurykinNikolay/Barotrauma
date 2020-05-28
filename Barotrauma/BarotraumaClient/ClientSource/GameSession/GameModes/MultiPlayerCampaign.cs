﻿using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Barotrauma
{
    partial class MultiPlayerCampaign : CampaignMode
    {
        public bool SuppressStateSending = false;

        private UInt16 startWatchmanID, endWatchmanID;

        public static void StartCampaignSetup(IEnumerable<string> saveFiles)
        {
            var parent = GameMain.NetLobbyScreen.CampaignSetupFrame;
            parent.ClearChildren();
            parent.Visible = true;
            GameMain.NetLobbyScreen.HighlightMode(2);

            var layout = new GUILayoutGroup(new RectTransform(Vector2.One, parent.RectTransform, Anchor.Center))
            {
                Stretch = true
            };

            var buttonContainer = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.07f), layout.RectTransform) { RelativeOffset = new Vector2(0.0f, 0.1f) }, isHorizontal: true)
            {
                RelativeSpacing = 0.02f
            };

            var campaignContainer = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.9f), layout.RectTransform, Anchor.BottomLeft), style: "InnerFrame")
            {
                CanBeFocused = false
            };
            
            var newCampaignContainer = new GUIFrame(new RectTransform(new Vector2(0.95f, 0.95f), campaignContainer.RectTransform, Anchor.Center), style: null);
            var loadCampaignContainer = new GUIFrame(new RectTransform(new Vector2(0.95f, 0.95f), campaignContainer.RectTransform, Anchor.Center), style: null);

            var campaignSetupUI = new CampaignSetupUI(true, newCampaignContainer, loadCampaignContainer, null, saveFiles);

            var newCampaignButton = new GUIButton(new RectTransform(new Vector2(0.5f, 1.0f), buttonContainer.RectTransform),
                TextManager.Get("NewCampaign"), style: "GUITabButton")
            {
                Selected = true
            };

            var loadCampaignButton = new GUIButton(new RectTransform(new Vector2(0.5f, 1.00f), buttonContainer.RectTransform),
                TextManager.Get("LoadCampaign"), style: "GUITabButton");

            newCampaignButton.OnClicked = (btn, obj) =>
            {
                newCampaignButton.Selected = true;
                loadCampaignButton.Selected = false;
                newCampaignContainer.Visible = true;
                loadCampaignContainer.Visible = false;
                return true;
            };
            loadCampaignButton.OnClicked = (btn, obj) =>
            {
                newCampaignButton.Selected = false;
                loadCampaignButton.Selected = true;
                newCampaignContainer.Visible = false;
                loadCampaignContainer.Visible = true;
                return true;
            };
            loadCampaignContainer.Visible = false;

            GUITextBlock.AutoScaleAndNormalize(newCampaignButton.TextBlock, loadCampaignButton.TextBlock);
            
            campaignSetupUI.StartNewGame = GameMain.Client.SetupNewCampaign;
            campaignSetupUI.LoadGame = GameMain.Client.SetupLoadCampaign;
        }

        public override void Update(float deltaTime)
        {
            base.Update(deltaTime);

            if (startWatchmanID > 0 && startWatchman == null)
            {
                startWatchman = Entity.FindEntityByID(startWatchmanID) as Character;
                if (startWatchman != null) { InitializeWatchman(startWatchman); }
            }
            if (endWatchmanID > 0 && endWatchman == null)
            {
                endWatchman = Entity.FindEntityByID(endWatchmanID) as Character;
                if (endWatchman != null) { InitializeWatchman(endWatchman); }
            }
        }
        
        protected override void WatchmanInteract(Character watchman, Character interactor)
        {
            if ((watchman.Submarine == Level.Loaded.StartOutpost && !Submarine.MainSub.AtStartPosition) ||
                (watchman.Submarine == Level.Loaded.EndOutpost && !Submarine.MainSub.AtEndPosition))
            {
                return;
            }

            if (GUIMessageBox.MessageBoxes.Any(mbox => mbox.UserData as string == "watchmanprompt"))
            {
                return;
            }

            if (GameMain.Client != null && interactor == Character.Controlled)
            {
                var msgBox = new GUIMessageBox("", TextManager.GetWithVariable("CampaignEnterOutpostPrompt", "[locationname]",
                    Submarine.MainSub.AtStartPosition ? Map.CurrentLocation.Name : Map.SelectedLocation.Name),                    
                    new string[] { TextManager.Get("Yes"), TextManager.Get("No") })
                {
                    UserData = "watchmanprompt"
                };
                msgBox.Buttons[0].OnClicked = (btn, userdata) =>
                {
                    GameMain.Client.RequestRoundEnd();
                    return true;
                };
                msgBox.Buttons[0].OnClicked += msgBox.Close;
                msgBox.Buttons[1].OnClicked += msgBox.Close;            
            }
        }

        public void ClientWrite(IWriteMessage msg)
        {
            System.Diagnostics.Debug.Assert(map.Locations.Count < UInt16.MaxValue);

            msg.Write(map.SelectedLocationIndex == -1 ? UInt16.MaxValue : (UInt16)map.SelectedLocationIndex);
            msg.Write(map.SelectedMissionIndex == -1 ? byte.MaxValue : (byte)map.SelectedMissionIndex);
            msg.Write(PurchasedHullRepairs);
            msg.Write(PurchasedItemRepairs);
            msg.Write(PurchasedLostShuttles);

            msg.Write((UInt16)CargoManager.PurchasedItems.Count);
            foreach (PurchasedItem pi in CargoManager.PurchasedItems)
            {
                msg.Write(pi.ItemPrefab.Identifier);
                msg.WriteRangedInteger(pi.Quantity, 0, 100);
            }
        }

        //static because we may need to instantiate the campaign if it hasn't been done yet
        public static void ClientRead(IReadMessage msg)
        {
            byte campaignID = msg.ReadByte();
            UInt16 updateID = msg.ReadUInt16();
            UInt16 saveID = msg.ReadUInt16();
            string mapSeed = msg.ReadString();
            UInt16 currentLocIndex = msg.ReadUInt16();
            UInt16 selectedLocIndex = msg.ReadUInt16();
            byte selectedMissionIndex = msg.ReadByte();

            UInt16 startWatchmanID = msg.ReadUInt16();
            UInt16 endWatchmanID = msg.ReadUInt16();

            int money = msg.ReadInt32();
            bool purchasedHullRepairs = msg.ReadBoolean();
            bool purchasedItemRepairs = msg.ReadBoolean();
            bool purchasedLostShuttles = msg.ReadBoolean();

            UInt16 purchasedItemCount = msg.ReadUInt16();
            List<PurchasedItem> purchasedItems = new List<PurchasedItem>();
            for (int i = 0; i < purchasedItemCount; i++)
            {
                string itemPrefabIdentifier = msg.ReadString();
                int itemQuantity = msg.ReadRangedInteger(0, CargoManager.MaxQuantity);
                purchasedItems.Add(new PurchasedItem(ItemPrefab.Prefabs[itemPrefabIdentifier], itemQuantity));
            }

            bool hasCharacterData = msg.ReadBoolean();
            CharacterInfo myCharacterInfo = null;
            if (hasCharacterData)
            {
                myCharacterInfo = CharacterInfo.ClientRead(CharacterPrefab.HumanSpeciesName, msg);
            }
            
            MultiPlayerCampaign campaign = GameMain.GameSession?.GameMode as MultiPlayerCampaign;
            if (campaign == null || campaignID != campaign.CampaignID)
            {
                string savePath = SaveUtil.CreateSavePath(SaveUtil.SaveType.Multiplayer);

                GameMain.GameSession = new GameSession(null, savePath,
                    GameModePreset.List.Find(g => g.Identifier == "multiplayercampaign"));

                campaign = ((MultiPlayerCampaign)GameMain.GameSession.GameMode);
                campaign.CampaignID = campaignID;
                campaign.GenerateMap(mapSeed);
                GameMain.NetLobbyScreen.ToggleCampaignMode(true);
            }


            //server has a newer save file
            if (NetIdUtils.IdMoreRecent(saveID, campaign.PendingSaveID))
            {
                /*//stop any active campaign save transfers, they're outdated now
                List<FileReceiver.FileTransferIn> saveTransfers = 
                    GameMain.Client.FileReceiver.ActiveTransfers.FindAll(t => t.FileType == FileTransferType.CampaignSave);

                foreach (var transfer in saveTransfers)
                {
                    GameMain.Client.FileReceiver.StopTransfer(transfer);                    
                }

                GameMain.Client.RequestFile(FileTransferType.CampaignSave, null, null);*/
                campaign.PendingSaveID = saveID;
            }
            
            if (NetIdUtils.IdMoreRecent(updateID, campaign.lastUpdateID))
            {
                campaign.SuppressStateSending = true;

                //we need to have the latest save file to display location/mission/store
                if (campaign.LastSaveID == saveID)
                {
                    campaign.Map.SetLocation(currentLocIndex == UInt16.MaxValue ? -1 : currentLocIndex);
                    campaign.Map.SelectLocation(selectedLocIndex == UInt16.MaxValue ? -1 : selectedLocIndex);
                    campaign.Map.SelectMission(selectedMissionIndex);
                    campaign.CargoManager.SetPurchasedItems(purchasedItems);
                }

                campaign.startWatchmanID = startWatchmanID;
                campaign.endWatchmanID = endWatchmanID;

                campaign.Money = money;
                campaign.PurchasedHullRepairs = purchasedHullRepairs;
                campaign.PurchasedItemRepairs = purchasedItemRepairs;
                campaign.PurchasedLostShuttles = purchasedLostShuttles;

                if (myCharacterInfo != null)
                {
                    GameMain.Client.CharacterInfo = myCharacterInfo;
                    GameMain.NetLobbyScreen.SetCampaignCharacterInfo(myCharacterInfo);
                }
                else
                {
                    GameMain.NetLobbyScreen.SetCampaignCharacterInfo(null);
                }

                campaign.lastUpdateID = updateID;
                campaign.SuppressStateSending = false;
            }
        }

        public override void Save(XElement element)
        {
            //do nothing, the clients get the save files from the server
        }
    }
}
