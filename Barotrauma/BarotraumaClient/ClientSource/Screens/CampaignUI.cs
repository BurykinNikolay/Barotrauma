﻿using Barotrauma.Extensions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Barotrauma
{
    class CampaignUI
    {
        public enum Tab { Map, Crew, Store, Repair }
        private Tab selectedTab;
        private GUIFrame[] tabs;
        private GUIFrame topPanel;

        private GUIListBox characterList;

        private Point prevResolution;

        private MapEntityCategory selectedItemCategory = MapEntityCategory.Equipment;

        private GUIListBox myItemList;
        private GUIListBox storeItemList;
        private GUITextBox searchBox;

        private GUIComponent missionPanel;
        private GUIComponent selectedLocationInfo;
        private GUIListBox selectedMissionInfo;

        private GUIButton repairHullsButton, replaceShuttlesButton, repairItemsButton;

        private GUIFrame characterPreviewFrame;

        private bool displayMissionPanelInMapTab;

        private readonly List<GUIButton> tabButtons = new List<GUIButton>();
        private readonly List<GUIButton> itemCategoryButtons = new List<GUIButton>();
        private readonly List<GUITickBox> missionTickBoxes = new List<GUITickBox>();
        private GUIRadioButtonGroup missionRadioButtonGroup = new GUIRadioButtonGroup();

        private Location selectedLocation;

        public Action StartRound;
        public Action<Location, LocationConnection> OnLocationSelected;

        public Level SelectedLevel { get; private set; }

        public GUIComponent MapContainer { get; private set; }
        
        public GUIButton StartButton { get; private set; }

        public CampaignMode Campaign { get; }

        public CampaignUI(CampaignMode campaign, GUIComponent parent)
        {
            this.Campaign = campaign;

            var container = new GUIFrame(new RectTransform(Vector2.One, parent.RectTransform), style: null);

            CreateUI(container);

            campaign.Map.OnLocationSelected += SelectLocation;
            campaign.Map.OnLocationChanged += (prevLocation, newLocation) => UpdateLocationView(newLocation);
            campaign.Map.OnMissionSelected += (connection, mission) => 
            {
                var selectedTickBox = (missionRadioButtonGroup.UserData as List<Mission>).FindIndex(m => m == mission);
                if (selectedTickBox >= 0)
                {
                    missionRadioButtonGroup.Selected = selectedTickBox;
                }
            };
            campaign.CargoManager.OnItemsChanged += RefreshMyItems;
        }

        private void CreateUI(GUIComponent container)
        {
            container.ClearChildren();

            MapContainer = new GUICustomComponent(new RectTransform(Vector2.One, container.RectTransform), DrawMap, UpdateMap);
            new GUIFrame(new RectTransform(Vector2.One, MapContainer.RectTransform), style: "InnerGlow", color: Color.Black * 0.9f)
            {
                CanBeFocused = false
            };

            // top panel -------------------------------------------------------------------------

            topPanel = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.15f), container.RectTransform, Anchor.TopCenter), style: null)
            {
                CanBeFocused = false
            };
            var topPanelContent = new GUIFrame(new RectTransform(new Vector2(0.95f, 0.9f), topPanel.RectTransform, Anchor.BottomCenter), style: null)
            {
                CanBeFocused = false
            };

            var outpostBtn = new GUIButton(new RectTransform(new Vector2(0.15f, 0.55f), topPanelContent.RectTransform),
                TextManager.Get("Outpost"), textAlignment: Alignment.Center, style: "GUISlopedHeader")
            {
                OnClicked = (btn, userdata) => { SelectTab(Tab.Map); return true; }
            };
            outpostBtn.TextBlock.Font = GUI.LargeFont;
            outpostBtn.TextBlock.AutoScaleHorizontal = true;

            var tabButtonContainer = new GUILayoutGroup(new RectTransform(new Vector2(0.4f, 0.4f), topPanelContent.RectTransform, Anchor.BottomLeft), isHorizontal: true);

            int i = 0;
            var tabValues = Enum.GetValues(typeof(Tab));
            foreach (Tab tab in tabValues)
            {
                var tabButton = new GUIButton(new RectTransform(new Vector2(0.25f, 1.0f), tabButtonContainer.RectTransform),
                    "",
                    style: i == 0 ? "GUISlopedTabButtonLeft" : (i == tabValues.Length - 1 ? "GUISlopedTabButtonRight" : "GUISlopedTabButtonMid"))
                {
                    UserData = tab,
                    OnClicked = (btn, userdata) => { SelectTab((Tab)userdata); return true; },
                    Selected = tab == Tab.Map
                };
                var buttonSprite = tabButton.Style.Sprites[GUIComponent.ComponentState.None][0];
                tabButton.RectTransform.MaxSize = new Point(
                    (int)(tabButton.Rect.Height * (buttonSprite.Sprite.size.X / buttonSprite.Sprite.size.Y)), int.MaxValue);

                //the text needs to be positioned differently in the buttons at the edges due to the "slopes" in the button
                if (i == 0 || i == tabValues.Length - 1)
                {
                    new GUITextBlock(new RectTransform(new Vector2(0.8f, 0.9f), tabButton.RectTransform, i == 0 ? Anchor.CenterLeft : Anchor.CenterRight) { RelativeOffset = new Vector2(0.05f, 0.0f) },
                        TextManager.Get(tab.ToString()), textColor: tabButton.TextColor, font: GUI.LargeFont, textAlignment: Alignment.Center, style: null)
                    {
                        UserData = "buttontext",
                        Padding = new Vector4(GUI.Scale * 1)
                    };
                }
                else
                {
                    new GUITextBlock(new RectTransform(new Vector2(0.7f, 0.9f), tabButton.RectTransform, Anchor.Center),
                        TextManager.Get(tab.ToString()), textColor: tabButton.TextColor, font: GUI.LargeFont, textAlignment: Alignment.Center, style: null)
                    {
                        UserData = "buttontext",
                        Padding = new Vector4(GUI.Scale * 1)
                    };
                }

                tabButtons.Add(tabButton);
                i++;
            }
            GUITextBlock.AutoScaleAndNormalize(tabButtons.Select(t => t.GetChildByUserData("buttontext") as GUITextBlock));
            tabButtons.FirstOrDefault().RectTransform.SizeChanged += () =>
            {
                GUITextBlock.AutoScaleAndNormalize(tabButtons.Select(t => t.GetChildByUserData("buttontext") as GUITextBlock), defaultScale: 1.0f);
            };

            // crew tab -------------------------------------------------------------------------

            tabs = new GUIFrame[Enum.GetValues(typeof(Tab)).Length];
            tabs[(int)Tab.Crew] = new GUIFrame(new RectTransform(new Vector2(0.3f, 0.7f), container.RectTransform, Anchor.TopLeft)
            {
                RelativeOffset = new Vector2(0.0f, topPanel.RectTransform.RelativeSize.Y)
            }, color: Color.Black * 0.9f);
            new GUIFrame(new RectTransform(new Vector2(1.25f, 1.25f), tabs[(int)Tab.Crew].RectTransform, Anchor.Center), style: "OuterGlow", color: Color.Black * 0.7f)
            {
                UserData = "outerglow",
                CanBeFocused = false
            };

            var crewContent = new GUILayoutGroup(new RectTransform(new Vector2(0.9f, 0.95f), tabs[(int)Tab.Crew].RectTransform, Anchor.Center))
            {
                Stretch = true,
                RelativeSpacing = 0.02f
            };

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.1f), crewContent.RectTransform), "", font: GUI.LargeFont)
            {
                TextGetter = GetMoney
            };

            characterList = new GUIListBox(new RectTransform(new Vector2(1.0f, 0.9f), crewContent.RectTransform))
            {
                OnSelected = SelectCharacter
            };

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.1f), characterList.Content.RectTransform),
                TextManager.Get("CampaignMenuCrew"), font: GUI.LargeFont)
            {
                UserData = "mycrew",
                CanBeFocused = false,
                AutoScaleHorizontal = true
            };
            if (Campaign is SinglePlayerCampaign)
            {
                new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.1f), characterList.Content.RectTransform),
                    TextManager.Get("CampaignMenuHireable"), font: GUI.LargeFont)
                {
                    UserData = "hire",
                    CanBeFocused = false,
                    AutoScaleHorizontal = true
                };
            }

            // store tab -------------------------------------------------------------------------

            tabs[(int)Tab.Store] = new GUIFrame(new RectTransform(new Vector2(0.5f, 0.7f), container.RectTransform, Anchor.TopLeft)
            {
                RelativeOffset = new Vector2(0.1f, topPanel.RectTransform.RelativeSize.Y)
            }, color: Color.Black * 0.9f);
            new GUIFrame(new RectTransform(new Vector2(1.25f, 1.25f), tabs[(int)Tab.Store].RectTransform, Anchor.Center), style: "OuterGlow", color: Color.Black * 0.7f)
            {
                UserData = "outerglow",
                CanBeFocused = false
            };

            var storeContent = new GUILayoutGroup(new RectTransform(new Vector2(0.9f, 0.9f), tabs[(int)Tab.Store].RectTransform, Anchor.Center))
            {
                UserData = "content",
                Stretch = true,
                RelativeSpacing = 0.015f
            };

            var storeContentTop = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.1f), storeContent.RectTransform) { MinSize = new Point(0, (int)(30 * GUI.Scale)) }, isHorizontal: true, childAnchor: Anchor.CenterLeft)
            {
                Stretch = true
            };

            new GUITextBlock(new RectTransform(new Vector2(0.5f, 1.0f), storeContentTop.RectTransform), "", font: GUI.LargeFont)
            {
                TextGetter = GetMoney
            };
            var filterContainer = new GUILayoutGroup(new RectTransform(new Vector2(0.5f, 0.4f), storeContentTop.RectTransform) { MinSize = new Point(0, (int)(25 * GUI.Scale)) }, isHorizontal: true)
            {
                Stretch = true
            };
            var searchTitle = new GUITextBlock(new RectTransform(new Vector2(0.001f, 1.0f), filterContainer.RectTransform), TextManager.Get("serverlog.filter"), textAlignment: Alignment.CenterLeft, font: GUI.Font);
            searchBox = new GUITextBox(new RectTransform(new Vector2(1.0f, 1.0f), filterContainer.RectTransform), createClearButton: true);
            searchBox.OnSelected += (sender, userdata) => { searchTitle.Visible = false; };
            searchBox.OnDeselected += (sender, userdata) => { searchTitle.Visible = true; };
            searchBox.OnTextChanged += (textBox, text) => { FilterStoreItems(null, text); return true; };

            var storeItemLists = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.8f), storeContent.RectTransform), isHorizontal: true)
            {
                RelativeSpacing = 0.03f,
                Stretch = true
            };
            myItemList = new GUIListBox(new RectTransform(new Vector2(0.5f, 1.0f), storeItemLists.RectTransform))
            {
                AutoHideScrollBar = false
            };
            storeItemList = new GUIListBox(new RectTransform(new Vector2(0.5f, 1.0f), storeItemLists.RectTransform))
            {
                AutoHideScrollBar = false,
                OnSelected = BuyItem
            };

            var categoryButtonContainer = new GUILayoutGroup(new RectTransform(new Vector2(0.1f, 0.9f), tabs[(int)Tab.Store].RectTransform, Anchor.CenterLeft, Pivot.CenterRight))
            {
                RelativeSpacing = 0.02f
            };

            List<MapEntityCategory> itemCategories = Enum.GetValues(typeof(MapEntityCategory)).Cast<MapEntityCategory>().ToList();
            //don't show categories with no buyable items
            itemCategories.RemoveAll(c =>
                !ItemPrefab.Prefabs.Any(ep => ep.Category.HasFlag(c) && ep.CanBeBought));
            foreach (MapEntityCategory category in itemCategories)
            {
                var categoryButton = new GUIButton(new RectTransform(new Point(categoryButtonContainer.Rect.Width, categoryButtonContainer.Rect.Width), categoryButtonContainer.RectTransform),
                    "", style: "ItemCategory" + category.ToString())
                {
                    UserData = category,
                    OnClicked = (btn, userdata) =>
                    {
                        MapEntityCategory newCategory = (MapEntityCategory)userdata;
                        if (newCategory != selectedItemCategory)
                        {
                            searchBox.Text = "";
                            storeItemList.ScrollBar.BarScroll = 0f;
                        }

                        FilterStoreItems((MapEntityCategory)userdata, searchBox.Text);
                        return true;
                    }
                };
                itemCategoryButtons.Add(categoryButton);

                categoryButton.RectTransform.SizeChanged += () =>
                {
                    var sprite = categoryButton.Frame.sprites[GUIComponent.ComponentState.None].First();
                    categoryButton.RectTransform.NonScaledSize =
                        new Point(categoryButton.Rect.Width, (int)(categoryButton.Rect.Width * ((float)sprite.Sprite.SourceRect.Height / sprite.Sprite.SourceRect.Width)));
                };

                new GUITextBlock(new RectTransform(new Vector2(0.95f, 0.256f), categoryButton.RectTransform, Anchor.BottomCenter) { RelativeOffset = new Vector2(0.0f, 0.02f) },
                   TextManager.Get("MapEntityCategory." + category), textAlignment: Alignment.Center, textColor: categoryButton.TextColor)
                {
                    Padding = Vector4.Zero,
                    AutoScaleHorizontal = true,
                    Color = Color.Transparent,
                    HoverColor = Color.Transparent,
                    PressedColor = Color.Transparent,
                    SelectedColor = Color.Transparent,
                    CanBeFocused = true
                };
            }
            FillStoreItemList();
            FilterStoreItems(MapEntityCategory.Equipment, "");

            // repair tab -------------------------------------------------------------------------

            tabs[(int)Tab.Repair] = new GUIFrame(new RectTransform(new Vector2(0.35f, 0.5f), container.RectTransform, Anchor.TopLeft)
            {
                RelativeOffset = new Vector2(0.02f, topPanel.RectTransform.RelativeSize.Y)
            }, color: Color.Black * 0.9f);
            new GUIFrame(new RectTransform(new Vector2(1.25f, 1.25f), tabs[(int)Tab.Repair].RectTransform, Anchor.Center), style: "OuterGlow", color: Color.Black * 0.7f)
            {
                UserData = "outerglow",
                CanBeFocused = false
            };

            var repairContent = new GUILayoutGroup(new RectTransform(new Vector2(0.9f, 0.85f), tabs[(int)Tab.Repair].RectTransform, Anchor.Center))
            {
                RelativeSpacing = 0.05f,
                Stretch = true
            };
            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.2f), repairContent.RectTransform), "", font: GUI.LargeFont)
            {
                TextGetter = GetMoney
            };

            // repair hulls -----------------------------------------------

            var repairHullsHolder = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.5f), repairContent.RectTransform), childAnchor: Anchor.TopRight)
            {
                RelativeSpacing = 0.05f,
                Stretch = true
            };
            new GUIImage(new RectTransform(new Vector2(0.3f, 1.0f), repairHullsHolder.RectTransform, Anchor.CenterLeft), "RepairHullButton")
            {
                IgnoreLayoutGroups = true,
                CanBeFocused = false
            };
            var repairHullsLabel = new GUITextBlock(new RectTransform(new Vector2(0.7f, 0.3f), repairHullsHolder.RectTransform), TextManager.Get("RepairAllWalls"), textAlignment: Alignment.Right, font: GUI.SubHeadingFont)
            {
                ForceUpperCase = true
            };
            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.3f), repairHullsHolder.RectTransform), CampaignMode.HullRepairCost.ToString(), textAlignment: Alignment.Right, font: GUI.SubHeadingFont);
            repairHullsButton = new GUIButton(new RectTransform(new Vector2(0.4f, 0.3f), repairHullsHolder.RectTransform) { MinSize = new Point(140, 0) }, TextManager.Get("Repair"))
            {
                OnClicked = (btn, userdata) =>
                {
                    if (Campaign.PurchasedHullRepairs)
                    {
                        Campaign.Money += CampaignMode.HullRepairCost;
                        Campaign.PurchasedHullRepairs = false;
                    }
                    else
                    {
                        if (Campaign.Money >= CampaignMode.HullRepairCost)
                        {
                            Campaign.Money -= CampaignMode.HullRepairCost;
                            Campaign.PurchasedHullRepairs = true;
                        }
                    }
                    GameMain.Client?.SendCampaignState();
                    btn.GetChild<GUITickBox>().Selected = Campaign.PurchasedHullRepairs;

                    return true;
                }
            };
            new GUITickBox(new RectTransform(new Vector2(0.65f), repairHullsButton.RectTransform, Anchor.CenterLeft) { AbsoluteOffset = new Point(10, 0) }, "")
            {
                CanBeFocused = false
            };

            // repair items -------------------------------------------

            var repairItemsHolder = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.5f), repairContent.RectTransform), childAnchor: Anchor.TopRight)
            {
                RelativeSpacing = 0.05f,
                Stretch = true
            };
            new GUIImage(new RectTransform(new Vector2(0.3f, 1.0f), repairItemsHolder.RectTransform, Anchor.CenterLeft), "RepairItemsButton")
            {
                IgnoreLayoutGroups = true,
                CanBeFocused = false
            };
            var repairItemsLabel = new GUITextBlock(new RectTransform(new Vector2(0.7f, 0.3f), repairItemsHolder.RectTransform), TextManager.Get("RepairAllItems"), textAlignment: Alignment.Right, font: GUI.SubHeadingFont)
            {
                ForceUpperCase = true
            };
            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.3f), repairItemsHolder.RectTransform), CampaignMode.ItemRepairCost.ToString(), textAlignment: Alignment.Right, font: GUI.SubHeadingFont);
            repairItemsButton = new GUIButton(new RectTransform(new Vector2(0.4f, 0.3f), repairItemsHolder.RectTransform) { MinSize = new Point(140, 0) }, TextManager.Get("Repair"))
            {
                OnClicked = (btn, userdata) =>
                {
                    if (Campaign.PurchasedItemRepairs)
                    {
                        Campaign.Money += CampaignMode.ItemRepairCost;
                        Campaign.PurchasedItemRepairs = false;
                    }
                    else
                    {
                        if (Campaign.Money >= CampaignMode.ItemRepairCost)
                        {
                            Campaign.Money -= CampaignMode.ItemRepairCost;
                            Campaign.PurchasedItemRepairs = true;
                        }
                    }
                    GameMain.Client?.SendCampaignState();
                    btn.GetChild<GUITickBox>().Selected = Campaign.PurchasedItemRepairs;

                    return true;
                }
            };
            new GUITickBox(new RectTransform(new Vector2(0.65f), repairItemsButton.RectTransform, Anchor.CenterLeft) { AbsoluteOffset = new Point(10, 0) }, "")
            {
                CanBeFocused = false
            };

            // replace lost shuttles -------------------------------------------

            var replaceShuttlesHolder = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.5f), repairContent.RectTransform), childAnchor: Anchor.TopRight)
            {
                RelativeSpacing = 0.05f,
                Stretch = true
            };
            new GUIImage(new RectTransform(new Vector2(0.3f, 1.0f), replaceShuttlesHolder.RectTransform, Anchor.CenterLeft), "ReplaceShuttlesButton")
            {
                IgnoreLayoutGroups = true,
                CanBeFocused = false
            };
            var replaceShuttlesLabel = new GUITextBlock(new RectTransform(new Vector2(0.7f, 0.3f), replaceShuttlesHolder.RectTransform), TextManager.Get("ReplaceLostShuttles"), textAlignment: Alignment.Right, font: GUI.SubHeadingFont)
            {
                ForceUpperCase = true
            };
            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.3f), replaceShuttlesHolder.RectTransform), CampaignMode.ShuttleReplaceCost.ToString(), textAlignment: Alignment.Right, font: GUI.SubHeadingFont);
            replaceShuttlesButton = new GUIButton(new RectTransform(new Vector2(0.4f, 0.3f), replaceShuttlesHolder.RectTransform) { MinSize = new Point(140, 0) }, TextManager.Get("ReplaceShuttles"))
            {
                OnClicked = (btn, userdata) =>
                {
                    if (GameMain.GameSession?.SubmarineInfo != null &&
                        GameMain.GameSession.SubmarineInfo.LeftBehindSubDockingPortOccupied)
                    {
                        new GUIMessageBox("", TextManager.Get("ReplaceShuttleDockingPortOccupied"));
                        return true;
                    }

                    if (Campaign.PurchasedLostShuttles)
                    {
                        Campaign.Money += CampaignMode.ShuttleReplaceCost;
                        Campaign.PurchasedLostShuttles = false;
                    }
                    else
                    {
                        if (Campaign.Money >= CampaignMode.ShuttleReplaceCost)
                        {
                            Campaign.Money -= CampaignMode.ShuttleReplaceCost;
                            Campaign.PurchasedLostShuttles = true;
                        }
                    }
                    GameMain.Client?.SendCampaignState();
                    btn.GetChild<GUITickBox>().Selected = Campaign.PurchasedLostShuttles;

                    return true;
                }
            };
            new GUITickBox(new RectTransform(new Vector2(0.65f), replaceShuttlesButton.RectTransform, Anchor.CenterLeft) { AbsoluteOffset = new Point(10, 0) }, "")
            {
                CanBeFocused = false
            };
            GUITextBlock.AutoScaleAndNormalize(repairHullsLabel, repairItemsLabel, replaceShuttlesLabel);
            GUITextBlock.AutoScaleAndNormalize(repairHullsButton.GetChild<GUITickBox>().TextBlock, repairItemsButton.GetChild<GUITickBox>().TextBlock, replaceShuttlesButton.GetChild<GUITickBox>().TextBlock);


            // mission info -------------------------------------------------------------------------

            missionPanel = new GUIFrame(new RectTransform(new Vector2(0.3f, 0.5f), container.RectTransform, Anchor.TopRight)
            {
                RelativeOffset = new Vector2(0.0f, topPanel.RectTransform.RelativeSize.Y)
            }, color: Color.Black * 0.7f)
            {
                Visible = false
            };

            new GUIFrame(new RectTransform(new Vector2(1.25f, 1.25f), missionPanel.RectTransform, Anchor.Center), style: "OuterGlow", color: Color.Black * 0.7f)
            {
                UserData = "outerglow",
                CanBeFocused = false
            };

            new GUITextBlock(new RectTransform(new Vector2(0.5f, 0.15f), missionPanel.RectTransform, Anchor.TopRight, Pivot.BottomRight)
            { RelativeOffset = new Vector2(0.1f, -0.05f) }, TextManager.Get("Mission"),
                textAlignment: Alignment.Center, font: GUI.LargeFont, style: "GUISlopedHeader")
            {
                UserData = "missionlabel",
                AutoScaleHorizontal = true
            };
            var missionPanelContent = new GUILayoutGroup(new RectTransform(new Vector2(0.95f, 0.95f), missionPanel.RectTransform, Anchor.Center))
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };

            selectedLocationInfo = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.75f), missionPanelContent.RectTransform))
            {
                RelativeSpacing = 0.02f,
                Stretch = true
            };
            selectedMissionInfo = new GUIListBox(new RectTransform(new Vector2(0.9f, 0.25f), missionPanel.RectTransform, Anchor.BottomRight, Pivot.TopRight)
            { MinSize = new Point(0, (int)(150 * GUI.Scale)) })
            {
                Visible = false
            };
            selectedMissionInfo.RectTransform.MaxSize = new Point(int.MaxValue, selectedMissionInfo.Rect.Height * 2);
            new GUIFrame(new RectTransform(new Vector2(1.25f, 1.25f), selectedMissionInfo.RectTransform, Anchor.Center), style: "OuterGlow", color: Color.Black * 0.9f)
            {
                UserData = "outerglow",
                CanBeFocused = false
            };

            // -------------------------------------------------------------------------

            topPanel.RectTransform.SetAsLastChild();

            SelectTab(Tab.Map);

            UpdateLocationView(Campaign.Map.CurrentLocation);

            menuPanelParent?.ClearChildren();
            missionPanelParent?.ClearChildren();
            if (menuPanelParent != null)
            {
                SetMenuPanelParent(menuPanelParent);
            }
            if (missionPanelParent != null)
            {
                SetMissionPanelParent(missionPanelParent);
            }

            prevResolution = new Point(GameMain.GraphicsWidth, GameMain.GraphicsHeight);
        }

        private RectTransform missionPanelParent, menuPanelParent;

        public void SetMissionPanelParent(RectTransform parent)
        {
            missionPanel.RectTransform.Parent = parent;
            missionPanel.RectTransform.RelativeOffset = Vector2.Zero;
            missionPanel.RectTransform.RelativeSize = Vector2.One;
            var outerGlow = missionPanel.GetChildByUserData("outerglow");
            if (outerGlow != null) { outerGlow.Visible = false; }
            var label = missionPanel.GetChildByUserData("missionlabel");
            if (label != null) { label.Visible = false; }

            displayMissionPanelInMapTab = true;

            selectedMissionInfo.RectTransform.RelativeOffset = Vector2.Zero;
            selectedMissionInfo.RectTransform.SetPosition(Anchor.BottomLeft, Pivot.BottomRight);
            missionPanelParent = parent;
        }
        public void SetMenuPanelParent(RectTransform parent)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                var panel = tabs[i];
                if (panel == null) { continue; }
                panel.RectTransform.Parent = parent;
                panel.RectTransform.RelativeOffset = Vector2.Zero;
                panel.RectTransform.RelativeSize = Vector2.One;
                var outerGlow = panel.GetChildByUserData("outerglow");
                if (outerGlow != null) { outerGlow.Visible = false; }

                if (i == (int)Tab.Store)
                {
                    panel.RectTransform.RelativeSize *= new Vector2(1.5f, 1.0f);
                    panel.RectTransform.SetPosition(Anchor.TopRight);
                    var content = panel.GetChildByUserData("content");
                    if (content != null) { content.RectTransform.RelativeSize = Vector2.One; }
                    new GUIFrame(new RectTransform(new Vector2(1.107f, 1.0f), panel.RectTransform, Anchor.TopRight), style: null)
                    {
                        Color = Color.Black,
                        CanBeFocused = false
                    }.SetAsFirstChild();
                }
            }
            menuPanelParent = parent;
        }

        private void UpdateLocationView(Location location)
        {
            if (location == null)
            {
                string errorMsg = "Failed to update CampaignUI location view (location was null)\n" + Environment.StackTrace;
                DebugConsole.ThrowError(errorMsg);
                GameAnalyticsManager.AddErrorEventOnce("CampaignUI.UpdateLocationView:LocationNull", GameAnalyticsSDK.Net.EGAErrorSeverity.Error, errorMsg);
                return;
            }

            if (characterPreviewFrame != null)
            {
                characterPreviewFrame.Parent?.RemoveChild(characterPreviewFrame);
                characterPreviewFrame = null;
            }
            
            if (characterList != null)
            {
                if (Campaign is SinglePlayerCampaign)
                {
                    var hireableCharacters = location.GetHireableCharacters();
                    foreach (GUIComponent child in characterList.Content.Children.ToList())
                    {
                        if (child.UserData is CharacterInfo character)
                        {
                            if (GameMain.GameSession.CrewManager != null)
                            {
                                if (GameMain.GameSession.CrewManager.GetCharacterInfos().Contains(character)) { continue; }
                            }
                        }
                        else if (child.UserData as string == "mycrew" || child.UserData as string == "hire")
                        {
                            continue;
                        }
                        characterList.RemoveChild(child);
                    }
                    if (!hireableCharacters.Any())
                    {
                        new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.2f), characterList.Content.RectTransform), TextManager.Get("HireUnavailable"), textAlignment: Alignment.Center)
                        {
                            CanBeFocused = false
                        };
                    }
                    else
                    {
                        foreach (CharacterInfo c in hireableCharacters)
                        {
                            var frame = c.CreateCharacterFrame(characterList.Content, c.Name + " (" + c.Job.Name + ")", c);
                            new GUITextBlock(new RectTransform(Vector2.One, frame.RectTransform, Anchor.TopRight), c.Salary.ToString(), textAlignment: Alignment.CenterRight);
                        }
                    }
                }
                characterList.UpdateScrollBarSize();
            }

            RefreshMyItems();

            bool purchaseableItemsFound = false;
            foreach (ItemPrefab itemPrefab in ItemPrefab.Prefabs)
            {
                PriceInfo priceInfo = itemPrefab.GetPrice(Campaign.Map.CurrentLocation);
                if (priceInfo != null) { purchaseableItemsFound = true; break; }
            }

            //disable store tab if there's nothing to buy
            tabButtons.Find(btn => (Tab)btn.UserData == Tab.Store).Enabled = purchaseableItemsFound;

            if (selectedTab == Tab.Store && !purchaseableItemsFound)
            {
                //switch out from store tab if there's nothing to buy
                SelectTab(Tab.Map);
            }
            else
            {
                //refresh store view
                FillStoreItemList();

                MapEntityCategory? category = null;
                //only select a specific category if the search box is empty
                //(items from all categories are shown when searching)
                if (string.IsNullOrEmpty(searchBox.Text)) { category = selectedItemCategory; }
                FilterStoreItems(category, searchBox.Text);
            }
        }

        private void DrawMap(SpriteBatch spriteBatch, GUICustomComponent mapContainer)
        {
            if (GameMain.GraphicsWidth != prevResolution.X || GameMain.GraphicsHeight != prevResolution.Y)
            {
                CreateUI(MapContainer.Parent);
            }

            GameMain.GameSession?.Map?.Draw(spriteBatch, mapContainer);
        }

        private void UpdateMap(float deltaTime, GUICustomComponent mapContainer)
        {
            GameMain.GameSession?.Map?.Update(deltaTime, mapContainer);
        }
        
        public void UpdateCharacterLists()
        {
            //remove the player's crew from the listbox (everything between the "mycrew" and "hire" labels)
            foreach (GUIComponent child in characterList.Content.Children.ToList())
            {
                if (child.UserData as string == "mycrew")
                {
                    continue;
                }
                else if (child.UserData as string == "hire")
                {
                    break;
                }
                characterList.RemoveChild(child);
            }
            foreach (CharacterInfo c in GameMain.GameSession.CrewManager.GetCharacterInfos().Reverse())
            {
                var frame = c.CreateCharacterFrame(characterList.Content, c.Name + " (" + c.Job.Name + ") ", c);
                //add after the "mycrew" label
                frame.RectTransform.RepositionChildInHierarchy(1);
            }
            characterList.UpdateScrollBarSize();
        }

        public void SelectLocation(Location location, LocationConnection connection)
        {
            selectedLocationInfo.ClearChildren();
            //don't select the map panel if the tabs are displayed in the same place as the map, and we're looking at some other tab
            if (!displayMissionPanelInMapTab || selectedTab == Tab.Map)
            {
                SelectTab(Tab.Map);
                missionPanel.Visible = location != null;
            }

            selectedLocation = location;
            if (location == null) { return; }
            
            var container = selectedLocationInfo;
            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), container.RectTransform), location.Name, font: GUI.LargeFont)
            {
                AutoScaleHorizontal = true
            };
            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), container.RectTransform), location.Type.Name, font: GUI.SubHeadingFont);

            Sprite portrait = location.Type.GetPortrait(location.PortraitId);
            new GUIImage(new RectTransform(new Vector2(1.0f, 0.6f),
                container.RectTransform), portrait, scaleToFit: true);

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), container.RectTransform), TextManager.Get("SelectMission"), font: GUI.SubHeadingFont)
            {
                AutoScaleHorizontal = true
            };

            var missionFrame = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.3f), container.RectTransform), style: "InnerFrame");
            var missionContent = new GUILayoutGroup(new RectTransform(new Vector2(0.95f, 0.9f), missionFrame.RectTransform, Anchor.Center))
            {
                RelativeSpacing = 0.02f,
                Stretch = true
            };
            
            SelectedLevel = connection?.Level;
            if (connection != null)
            {
                List<Mission> availableMissions = Campaign.Map.CurrentLocation.GetMissionsInConnection(connection).ToList();
                if (!availableMissions.Contains(null)) { availableMissions.Add(null); }

                Mission selectedMission = Campaign.Map.CurrentLocation.SelectedMission != null && availableMissions.Contains(Campaign.Map.CurrentLocation.SelectedMission) ?
                    Campaign.Map.CurrentLocation.SelectedMission : null;
                missionTickBoxes.Clear();
                missionRadioButtonGroup = new GUIRadioButtonGroup
                {
                    UserData = availableMissions
                };

                for (int i = 0; i < availableMissions.Count; i++)
                {
                    var mission = availableMissions[i];
                    var tickBox = new GUITickBox(new RectTransform(new Vector2(0.65f, 0.1f), missionContent.RectTransform),
                       mission?.Name ?? TextManager.Get("NoMission"), style: "GUIRadioButton")
                    {
                        Enabled = GameMain.Client == null || GameMain.Client.HasPermission(Networking.ClientPermissions.ManageCampaign)
                    };
                    tickBox.Font = tickBox.Rect.Width < 150 ? GUI.SmallFont : GUI.Font;
                    tickBox.TextBlock.Wrap = true;
                    missionTickBoxes.Add(tickBox);
                    missionRadioButtonGroup.AddRadioButton(i, tickBox);
                }

                missionFrame.RectTransform.MinSize = 
                    new Point(0, (int)(missionContent.RectTransform.Children.Sum(c => c.MinSize.Y * 1.02f) / missionContent.RectTransform.RelativeSize.Y));

                if (GameMain.Client == null || GameMain.Client.HasPermission(Networking.ClientPermissions.ManageCampaign))
                {
                    missionRadioButtonGroup.OnSelect = (rbg, missionInd) =>
                    {
                        int ind = missionInd ?? -1;
                        if (ind < 0) { return; }
                        var mission = availableMissions[ind];
                        if (Campaign.Map.CurrentLocation.SelectedMission == mission) { return; }
                        if (rbg.Selected == missionInd) { return; }
                        RefreshMissionTab(mission);
                        if ((Campaign is MultiPlayerCampaign multiPlayerCampaign) && !multiPlayerCampaign.SuppressStateSending &&
                            GameMain.Client != null && GameMain.Client.HasPermission(Networking.ClientPermissions.ManageCampaign))
                        {
                            GameMain.Client?.SendCampaignState();
                        }
                    };
                }

                missionRadioButtonGroup.Selected = availableMissions.IndexOf(selectedMission);

                RefreshMissionTab(selectedMission);

                StartButton = new GUIButton(new RectTransform(new Vector2(0.3f, 0.7f), missionContent.RectTransform, Anchor.CenterRight),
                    TextManager.Get("StartCampaignButton"), style: "GUIButtonLarge")
                {
                    IgnoreLayoutGroups = true,
                    OnClicked = (GUIButton btn, object obj) => { StartRound?.Invoke(); return true; },
                    Enabled = true
                };
                if (GameMain.Client != null)
                {
                    StartButton.Visible = !GameMain.Client.GameStarted &&
                        (GameMain.Client.HasPermission(Networking.ClientPermissions.ManageRound) ||
                        GameMain.Client.HasPermission(Networking.ClientPermissions.ManageCampaign));
                }
            }

            OnLocationSelected?.Invoke(location, connection);
        }


        public void RefreshMissionTab(Mission selectedMission)
        {
            System.Diagnostics.Debug.Assert(
                selectedMission == null ||
                (GameMain.GameSession.Map?.SelectedConnection != null &&
                GameMain.GameSession.Map.CurrentLocation.AvailableMissions.Contains(selectedMission)));
            
            GameMain.GameSession.Map.CurrentLocation.SelectedMission = selectedMission;

            var selectedTickBoxIndex = (missionRadioButtonGroup.UserData as List<Mission>).FindIndex(m => m == selectedMission);
            if (selectedTickBoxIndex >= 0)
            {
                missionRadioButtonGroup.Selected = selectedTickBoxIndex;
            }

            selectedMissionInfo.ClearChildren();
            var container = selectedMissionInfo.Content;
            selectedMissionInfo.Visible = selectedMission != null;
            selectedMissionInfo.Spacing = (int)(10 * GUI.Scale);
            if (selectedMission == null) { return; }

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), container.RectTransform),
                selectedMission.Name, font: GUI.LargeFont)
            {
                AutoScaleHorizontal = true,
                CanBeFocused = false
            };
            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), container.RectTransform),
                TextManager.GetWithVariable("Reward", "[reward]", selectedMission.Reward.ToString()))
            {
                CanBeFocused = false
            };
            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.0f), container.RectTransform),
                selectedMission.Description, wrap: true)
            {
                CanBeFocused = false
            };

            //scale down mission info box if it's much taller than the text
            float missionInfoHeight = selectedMissionInfo.Content.Children.Sum(c => c.Rect.Height + selectedMissionInfo.Spacing);
            selectedMissionInfo.Content.Children.ForEach(c => c.RectTransform.IsFixedSize = true);
            selectedMissionInfo.RectTransform.Resize(new Point(selectedMissionInfo.Rect.Width, (int)(missionInfoHeight + 15 * GUI.Scale)));
            selectedMissionInfo.UpdateScrollBarSize();

            if (StartButton != null)
            {
                StartButton.Enabled = true;
                StartButton.Visible = GameMain.Client == null || 
                    GameMain.Client.HasPermission(Networking.ClientPermissions.ManageRound) || 
                    GameMain.Client.HasPermission(Networking.ClientPermissions.ManageCampaign);
            }
        }

        private GUIComponent CreateItemFrame(PurchasedItem pi, PriceInfo priceInfo, GUIListBox listBox)
        {
            GUIFrame frame = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.05f), listBox.Content.RectTransform), style: "ListBoxElement")
            {
                UserData = pi,
                ToolTip = pi.ItemPrefab.Description
            };
            frame.RectTransform.MinSize = new Point(0, (int)(GUI.Scale * 50));

            var content = new GUILayoutGroup(new RectTransform(new Vector2(0.95f, 1.0f), frame.RectTransform, Anchor.Center), 
                isHorizontal: true, childAnchor: Anchor.CenterLeft)
            {
                AbsoluteSpacing = (int)(5 * GUI.Scale),
                Stretch = true
            };

            ScalableFont font = listBox.Content.Rect.Width < 280 ? GUI.SmallFont : GUI.Font;

            Sprite itemIcon = pi.ItemPrefab.InventoryIcon ?? pi.ItemPrefab.sprite;
            if (itemIcon != null)
            {
                GUIImage img = new GUIImage(new RectTransform(new Point((int)(content.Rect.Height * 0.8f)), content.RectTransform), itemIcon, scaleToFit: true)
                {
                    Color = itemIcon == pi.ItemPrefab.InventoryIcon ? pi.ItemPrefab.InventoryIconColor : pi.ItemPrefab.SpriteColor
                };
                img.RectTransform.MaxSize = img.Rect.Size;
                //img.Scale = Math.Min(Math.Min(40.0f / img.SourceRect.Width, 40.0f / img.SourceRect.Height), 1.0f);
            }

            GUITextBlock textBlock = new GUITextBlock(new RectTransform(new Vector2(0.6f, 1.0f), content.RectTransform), 
                pi.ItemPrefab.Name, font: font)
            {
                ToolTip = pi.ItemPrefab.Description
            };

            new GUITextBlock(new RectTransform(new Vector2(0.2f, 1.0f), content.RectTransform),
                priceInfo.BuyPrice.ToString(), font: font, textAlignment: Alignment.CenterRight)
            {
                ToolTip = pi.ItemPrefab.Description
            };

            //If its the store menu, quantity will always be 0
            GUINumberInput amountInput = null;
            if (pi.Quantity > 0)
            {
                amountInput = new GUINumberInput(new RectTransform(new Vector2(0.3f, 1.0f), content.RectTransform),
                    GUINumberInput.NumberType.Int)
                {
                    MinValueInt = 0,
                    MaxValueInt = CargoManager.MaxQuantity,
                    UserData = pi,
                    IntValue = pi.Quantity
                };
                amountInput.TextBox.OnSelected += (sender, key) => { suppressBuySell = true; };
                amountInput.TextBox.OnDeselected += (sender, key) => { suppressBuySell = false; amountInput.OnValueChanged?.Invoke(amountInput); };
                amountInput.OnValueChanged += (numberInput) =>
                {
                    if (suppressBuySell) { return; }
                    PurchasedItem purchasedItem = numberInput.UserData as PurchasedItem;
                    if (GameMain.Client != null && !GameMain.Client.HasPermission(Networking.ClientPermissions.ManageCampaign))
                    {
                        numberInput.IntValue = purchasedItem.Quantity;
                        return;
                    }
                    //Attempting to buy
                    if (numberInput.IntValue > purchasedItem.Quantity)
                    {
                        int quantity = numberInput.IntValue - purchasedItem.Quantity;
                        //Cap the numberbox based on the amount we can afford.
                        quantity = Campaign.Money <= 0 ? 
                            0 : Math.Min((int)(Campaign.Money / (float)priceInfo.BuyPrice), quantity);
                        for (int i = 0; i < quantity; i++)
                        {
                            BuyItem(numberInput, purchasedItem);
                        }
                    }
                    //Attempting to sell
                    else
                    {
                        int quantity = purchasedItem.Quantity - numberInput.IntValue;
                        for (int i = 0; i < quantity; i++)
                        {
                            SellItem(numberInput, purchasedItem);
                        }
                    }
                };
                frame.HoverColor = frame.SelectedColor = Color.Transparent;
            }
            listBox.RecalculateChildren();
            content.Recalculate();
            content.RectTransform.RecalculateChildren(true, true);
            amountInput?.LayoutGroup.Recalculate();
            textBlock.Text = ToolBox.LimitString(textBlock.Text, textBlock.Font, textBlock.Rect.Width);
            //content.RectTransform.IsFixedSize = true;
            content.RectTransform.Children.ForEach(c => c.IsFixedSize = true);

            return frame;
        }

        private bool BuyItem(GUIComponent component, object obj)
        {
            if (!(obj is PurchasedItem pi) || pi.ItemPrefab == null) { return false; }

            if (GameMain.Client != null && !GameMain.Client.HasPermission(Networking.ClientPermissions.ManageCampaign))
            {
                return false;
            }

            var purchasedItem = Campaign.CargoManager.PurchasedItems.Find(pi2 => pi2.ItemPrefab == pi.ItemPrefab);
            if (purchasedItem != null && purchasedItem.Quantity >= CargoManager.MaxQuantity) { return false; }

            PriceInfo priceInfo = pi.ItemPrefab.GetPrice(Campaign.Map.CurrentLocation);
            if (priceInfo == null || priceInfo.BuyPrice > Campaign.Money) { return false; }
            
            Campaign.CargoManager.PurchaseItem(pi.ItemPrefab, 1);
            GameMain.Client?.SendCampaignState();

            return false;
        }

        private bool SellItem(GUIComponent component, object obj)
        {
            if (!(obj is PurchasedItem pi) || pi.ItemPrefab == null) { return false; }

            if (GameMain.Client != null && !GameMain.Client.HasPermission(Networking.ClientPermissions.ManageCampaign))
            {
                return false;
            }
            
            Campaign.CargoManager.SellItem(pi, 1);
            GameMain.Client?.SendCampaignState();

            return false;
        }

        private bool suppressBuySell;

        private void RefreshMyItems()
        {
            HashSet<GUIComponent> existingItemFrames = new HashSet<GUIComponent>();
            foreach (PurchasedItem pi in Campaign.CargoManager.PurchasedItems)
            {
                var itemFrame = myItemList.Content.Children.FirstOrDefault(c => 
                    c.UserData is PurchasedItem pi2 && pi.ItemPrefab == pi2.ItemPrefab);
                if (itemFrame == null)
                {
                    var priceInfo = pi.ItemPrefab.GetPrice(Campaign.Map.CurrentLocation);
                    if (priceInfo == null) { continue; }
                    itemFrame = CreateItemFrame(pi, priceInfo, myItemList);
                    itemFrame.Flash(GUI.Style.Green);                    
                }
                else
                {
                    itemFrame.UserData = itemFrame.GetChild(0).GetChild<GUINumberInput>().UserData = pi;
                }
                existingItemFrames.Add(itemFrame);

                suppressBuySell = true;
                var numInput = itemFrame.GetChild(0).GetChild<GUINumberInput>();
                if (numInput.IntValue != pi.Quantity) { itemFrame.Flash(GUI.Style.Green); }
                numInput.IntValue = (itemFrame.UserData as PurchasedItem).Quantity = pi.Quantity;
                suppressBuySell = false;
            }

            var removedItemFrames = myItemList.Content.Children.Except(existingItemFrames).ToList();
            foreach (GUIComponent removedItemFrame in removedItemFrames)
            {
                myItemList.Content.RemoveChild(removedItemFrame);
            }

            myItemList.Content.RectTransform.SortChildren((x, y) =>
                (x.GUIComponent.UserData as PurchasedItem).ItemPrefab.Name.CompareTo((y.GUIComponent.UserData as PurchasedItem).ItemPrefab.Name));
            myItemList.Content.RectTransform.SortChildren((x, y) =>
                (x.GUIComponent.UserData as PurchasedItem).ItemPrefab.Category.CompareTo((y.GUIComponent.UserData as PurchasedItem).ItemPrefab.Category));
            myItemList.UpdateScrollBarSize();
        }
        
        public void SelectTab(Tab tab)
        {
            selectedTab = tab;
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null)
                {
                    tabs[i].Visible = (int)selectedTab == i;
                }
            }
            
            missionPanel.Visible = tab == Tab.Map && selectedLocation != null;            

            foreach (GUIButton button in tabButtons)
            {
                button.Selected = (Tab)button.UserData == tab;
            }

            switch (selectedTab)
            {
                case Tab.Repair:
                    repairHullsButton.Enabled = 
                        (Campaign.PurchasedHullRepairs || Campaign.Money >= CampaignMode.HullRepairCost) &&
                        (GameMain.Client == null || GameMain.Client.HasPermission(Networking.ClientPermissions.ManageCampaign));
                    repairHullsButton.GetChild<GUITickBox>().Selected = Campaign.PurchasedHullRepairs;
                    repairItemsButton.Enabled = 
                        (Campaign.PurchasedItemRepairs || Campaign.Money >= CampaignMode.ItemRepairCost) &&
                        (GameMain.Client == null || GameMain.Client.HasPermission(Networking.ClientPermissions.ManageCampaign));
                    repairItemsButton.GetChild<GUITickBox>().Selected = Campaign.PurchasedItemRepairs;

                    if (GameMain.GameSession?.SubmarineInfo == null || !GameMain.GameSession.SubmarineInfo.SubsLeftBehind)
                    {
                        replaceShuttlesButton.Enabled = false;
                        replaceShuttlesButton.GetChild<GUITickBox>().Selected = false;
                    }
                    else
                    {
                        replaceShuttlesButton.Enabled =
                            (Campaign.PurchasedLostShuttles || Campaign.Money >= CampaignMode.ShuttleReplaceCost) &&
                            (GameMain.Client == null || GameMain.Client.HasPermission(Networking.ClientPermissions.ManageCampaign));
                        replaceShuttlesButton.GetChild<GUITickBox>().Selected = Campaign.PurchasedLostShuttles;
                    }
                    break;
            }
        }

        private void FillStoreItemList()
        {
            float prevStoreItemScroll = storeItemList.BarScroll;
            float prevMyItemScroll = myItemList.BarScroll;

            HashSet<GUIComponent> existingItemFrames = new HashSet<GUIComponent>();
            foreach (ItemPrefab itemPrefab in ItemPrefab.Prefabs)
            {
                PriceInfo priceInfo = itemPrefab.GetPrice(Campaign.Map.CurrentLocation);
                if (priceInfo == null) { continue; }

                var itemFrame = myItemList.Content.GetChildByUserData(priceInfo);
                if (itemFrame == null)
                {
                    itemFrame = CreateItemFrame(new PurchasedItem(itemPrefab, 0), priceInfo, storeItemList);
                }
                existingItemFrames.Add(itemFrame);
            }

            var removedItemFrames = storeItemList.Content.Children.Except(existingItemFrames).ToList();
            foreach (GUIComponent removedItemFrame in removedItemFrames)
            {
                storeItemList.Content.RemoveChild(removedItemFrame);
            }

            storeItemList.Content.RectTransform.SortChildren(
                (x, y) => (x.GUIComponent.UserData as PurchasedItem).ItemPrefab.Name.CompareTo((y.GUIComponent.UserData as PurchasedItem).ItemPrefab.Name));

            storeItemList.BarScroll = prevStoreItemScroll;
            myItemList.BarScroll = prevMyItemScroll;
        }

        private void FilterStoreItems(MapEntityCategory? category, string filter)
        {
            if (category.HasValue)
            {
                selectedItemCategory = category.Value;
            }
            foreach (GUIComponent child in storeItemList.Content.Children)
            {
                var item = child.UserData as PurchasedItem;
                if (item?.ItemPrefab?.Name == null) { continue; }
                child.Visible =
                    (!category.HasValue || item.ItemPrefab.Category.HasFlag(category.Value)) &&
                    (string.IsNullOrEmpty(filter) || item.ItemPrefab.Name.ToLower().Contains(searchBox.Text.ToLower()));
            }
            foreach (GUIButton btn in itemCategoryButtons)
            {
                btn.Selected = (MapEntityCategory)btn.UserData == selectedItemCategory;
            }
            storeItemList.UpdateScrollBarSize();
            //storeItemList.BarScroll = 0.0f;
        }

        public string GetMoney()
        {
            return TextManager.GetWithVariable("PlayerCredits", "[credits]", (GameMain.GameSession == null) ? "0" : string.Format(CultureInfo.InvariantCulture, "{0:N0}", Campaign.Money));
        }

        private bool SelectCharacter(GUIComponent component, object selection)
        {
            GUIComponent prevInfoFrame = null;
            foreach (GUIComponent child in tabs[(int)selectedTab].Children)
            {
                if (!(child.UserData is CharacterInfo)) { continue; }

                prevInfoFrame = child;
            }

            if (prevInfoFrame != null) { tabs[(int)selectedTab].RemoveChild(prevInfoFrame); }

            if (!(selection is CharacterInfo characterInfo)) { return false; }
            if (Character.Controlled != null && characterInfo == Character.Controlled.Info) { return false; }

            if (characterPreviewFrame == null || characterPreviewFrame.UserData != characterInfo)
            {
                characterPreviewFrame = new GUIFrame(new RectTransform(new Vector2(0.75f, 0.5f), tabs[(int)selectedTab].RectTransform, Anchor.TopRight, Pivot.TopLeft))
                {
                    UserData = characterInfo
                };
                var characterPreviewContent = new GUIFrame(new RectTransform(new Vector2(1.0f, 0.8f), characterPreviewFrame.RectTransform, Anchor.TopCenter) { RelativeOffset = new Vector2(0.0f, 0.02f) }, style: null);

                characterInfo.CreateInfoFrame(characterPreviewContent, true);
            }

            var currentCrew = GameMain.GameSession.CrewManager.GetCharacterInfos();
            if (currentCrew.Contains(characterInfo))
            {
                new GUIButton(new RectTransform(new Vector2(0.5f, 0.1f), characterPreviewFrame.RectTransform, Anchor.BottomCenter) { RelativeOffset = new Vector2(0.0f, 0.05f) }, 
                    TextManager.Get("FireButton"))
                {
                    Color = GUI.Style.Red,
                    UserData = characterInfo,
                    Enabled = currentCrew.Count() > 1, //can't fire if there's only one character in the crew
                    OnClicked = (btn, obj) =>
                    {
                        var confirmDialog = new GUIMessageBox(
                            TextManager.Get("FireWarningHeader"),
                            TextManager.GetWithVariable("FireWarningText", "[charactername]", ((CharacterInfo)obj).Name),
                            new string[] { TextManager.Get("Yes"), TextManager.Get("No") });
                        confirmDialog.Buttons[0].UserData = (CharacterInfo)obj;
                        confirmDialog.Buttons[0].OnClicked = FireCharacter;
                        confirmDialog.Buttons[0].OnClicked += confirmDialog.Close;
                        confirmDialog.Buttons[1].OnClicked = confirmDialog.Close;
                        return true;
                    }
                };
            }
            else
            {
                new GUIButton(new RectTransform(new Vector2(0.5f, 0.1f), characterPreviewFrame.RectTransform, Anchor.BottomCenter) { RelativeOffset = new Vector2(0.0f, 0.05f) },
                    TextManager.Get("HireButton"))
                {
                    Enabled = Campaign.Money >= characterInfo.Salary,
                    UserData = characterInfo,
                    OnClicked = HireCharacter
                };
            }

            return true;
        }

        private bool HireCharacter(GUIButton button, object selection)
        {
            if (!(selection is CharacterInfo characterInfo)) { return false; }

            if (!(Campaign is SinglePlayerCampaign spCampaign))
            {
                DebugConsole.ThrowError("Characters can only be hired in the single player campaign.\n" + Environment.StackTrace);
                return false;
            }

            if (spCampaign.TryHireCharacter(GameMain.GameSession.Map.CurrentLocation, characterInfo))
            {
                UpdateLocationView(GameMain.GameSession.Map.CurrentLocation);
                SelectCharacter(null, null);
                characterList.Content.RemoveChild(characterList.Content.FindChild(characterInfo));
                UpdateCharacterLists();
            }

            return false;
        }

        private bool FireCharacter(GUIButton button, object selection)
        {
            if (!(selection is CharacterInfo characterInfo)) return false;

            if (!(Campaign is SinglePlayerCampaign spCampaign))
            {
                DebugConsole.ThrowError("Characters can only be fired in the single player campaign.\n" + Environment.StackTrace);
                return false;
            }

            spCampaign.FireCharacter(characterInfo);
            SelectCharacter(null, null);
            UpdateCharacterLists();

            return false;
        }

    }
}
