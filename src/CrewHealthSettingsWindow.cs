using Barotrauma;
using Microsoft.Xna.Framework;
using System;

namespace CrewHealthDisplayMod
{
    public static class CrewHealthSettingsWindow
    {
        private static GUIFrame buttonContainer;
        private static GUIButton settingsButton;
        private static GUIMessageBox settingsBox;
        private static GUITextBlock scaleValueText;
        private static GUINumberInput scaleInput;
        private static GUINumberInput positionXInput;
        private static GUINumberInput positionYInput;
        private static GUINumberInput hideHealthyThresholdInput;
        private static GUITickBox hideAICrewTickBox;
        private static GUITickBox hideDeadCrewTickBox;
        private static GUITickBox hideHealthyCrewTickBox;
        private static GUITickBox alwaysShowSelfTickBox;
        private static bool refreshing;

        public static void DisposeUi()
        {
            if (settingsBox != null && !settingsBox.Closed)
            {
                settingsBox.Close();
            }
            ClearSettingsReferences();

            if (settingsButton != null)
            {
                GUI.RemoveFromUpdateList(settingsButton, true);
                settingsButton.RectTransform.Parent = null;
                settingsButton = null;
            }

            if (buttonContainer != null)
            {
                GUI.RemoveFromUpdateList(buttonContainer, true);
                buttonContainer.RectTransform.Parent = null;
                buttonContainer = null;
            }
        }

        public static void HideInteractiveUi()
        {
            if (settingsBox != null && !settingsBox.Closed)
            {
                settingsBox.Close();
            }
            ClearSettingsReferences();

            if (settingsButton != null)
            {
                settingsButton.Visible = false;
                settingsButton.Enabled = false;
            }
        }

        public static void CleanupClosedSettingsBox()
        {
            if (settingsBox != null && settingsBox.Closed)
            {
                ClearSettingsReferences();
            }
        }

        public static void Update(Rectangle settingsButtonRect)
        {
            UpdateSettingsButton(settingsButtonRect);
        }

        public static void UpdateGuiButtons()
        {
            if (buttonContainer == null) { return; }
            if (settingsBox != null && !settingsBox.Closed)
            {
                settingsBox.AddToGUIUpdateList(order: 1);
            }
            if (settingsButton != null)
            {
                settingsButton.AddToGUIUpdateList(order: 1);
            }
            buttonContainer.AddToGUIUpdateList(order: 1);
        }

        private static void ToggleSettingsWindow()
        {
            if (settingsBox != null && !settingsBox.Closed)
            {
                settingsBox.Close();
                settingsBox = null;
                return;
            }

            settingsBox = new GUIMessageBox("Crew Health Display", string.Empty,
                Array.Empty<LocalizedString>(),
                relativeSize: new Vector2(0.36f, 0.62f),
                minSize: new Point(620, 620))
            {
                DrawOnTop = true,
                Draggable = true
            };

            BuildSettingsWindow();
        }

        private static void BuildSettingsWindow()
        {
            if (settingsBox?.Content == null) { return; }
            bool isEditorMode = IsCharacterEditorSelected();

            var root = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.95f), settingsBox.Content.RectTransform), isHorizontal: false)
            {
                Stretch = true,
                RelativeSpacing = 0.008f
            };

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.06f), root.RectTransform),
                "Press Alt+U to show or hide the panel. Changes apply and save immediately.",
                textAlignment: Alignment.CenterLeft, wrap: true)
            {
                TextColor = Color.LightGray,
                CanBeFocused = false
            };

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.06f), root.RectTransform),
                "SIZE", font: GUIStyle.SmallFont, textAlignment: Alignment.CenterLeft)
            {
                TextColor = Color.LightGray,
                ForceUpperCase = ForceUpperCase.Yes,
                CanBeFocused = false
            };

            var scaleRow = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.085f), root.RectTransform), isHorizontal: true, childAnchor: Anchor.CenterLeft)
            {
                Stretch = true,
                RelativeSpacing = 0.025f
            };

            new GUITextBlock(new RectTransform(new Vector2(0.43f, 1.0f), scaleRow.RectTransform),
                $"Scale ({CrewHealthDisplaySettings.MinDisplayScale:0.00}-{CrewHealthDisplaySettings.MaxDisplayScale:0.00})", textAlignment: Alignment.CenterLeft)
            {
                CanBeFocused = false
            };

            scaleInput = new GUINumberInput(
                new RectTransform(new Vector2(0.34f, 0.90f), scaleRow.RectTransform),
                NumberType.Float,
                buttonVisibility: GUINumberInput.ButtonVisibility.ForceVisible)
            {
                Font = GUIStyle.SmallFont,
                MinValueFloat = CrewHealthDisplaySettings.MinDisplayScale,
                MaxValueFloat = CrewHealthDisplaySettings.MaxDisplayScale,
                ValueStep = 0.1f,
                DecimalsToDisplay = 2,
                FloatValue = CrewHealthDisplaySettings.DisplayScale,
                ToolTip = "Panel scale multiplier"
            };

            scaleValueText = new GUITextBlock(new RectTransform(new Vector2(0.18f, 1.0f), scaleRow.RectTransform),
                CrewHealthDisplaySettings.GetScaleLabel(), textAlignment: Alignment.Center)
            {
                CanBeFocused = false
            };
            BindImmediateApply(scaleInput);

            new GUITextBlock(new RectTransform(new Vector2(1.0f, 0.06f), root.RectTransform),
                "POSITION", font: GUIStyle.SmallFont, textAlignment: Alignment.CenterLeft)
            {
                TextColor = Color.LightGray,
                ForceUpperCase = ForceUpperCase.Yes,
                CanBeFocused = false
            };

            var xRow = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.085f), root.RectTransform), isHorizontal: true, childAnchor: Anchor.CenterLeft)
            {
                Stretch = true,
                RelativeSpacing = 0.025f
            };

            new GUITextBlock(new RectTransform(new Vector2(0.43f, 1.0f), xRow.RectTransform),
                "X (0 left, 1 right)", textAlignment: Alignment.CenterLeft)
            {
                CanBeFocused = false
            };

            positionXInput = new GUINumberInput(
                new RectTransform(new Vector2(0.34f, 0.90f), xRow.RectTransform),
                NumberType.Float,
                buttonVisibility: GUINumberInput.ButtonVisibility.ForceVisible)
            {
                Font = GUIStyle.SmallFont,
                MinValueFloat = 0.0f,
                MaxValueFloat = 1.0f,
                ValueStep = 0.1f,
                DecimalsToDisplay = 2,
                FloatValue = CrewHealthDisplaySettings.PositionX,
                ToolTip = "Normalized horizontal panel position"
            };
            BindImmediateApply(positionXInput);

            var yRow = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.085f), root.RectTransform), isHorizontal: true, childAnchor: Anchor.CenterLeft)
            {
                Stretch = true,
                RelativeSpacing = 0.025f
            };

            new GUITextBlock(new RectTransform(new Vector2(0.43f, 1.0f), yRow.RectTransform),
                "Y (0 top, 1 bottom)", textAlignment: Alignment.CenterLeft)
            {
                CanBeFocused = false
            };

            positionYInput = new GUINumberInput(
                new RectTransform(new Vector2(0.34f, 0.90f), yRow.RectTransform),
                NumberType.Float,
                buttonVisibility: GUINumberInput.ButtonVisibility.ForceVisible)
            {
                Font = GUIStyle.SmallFont,
                MinValueFloat = 0.0f,
                MaxValueFloat = 1.0f,
                ValueStep = 0.1f,
                DecimalsToDisplay = 2,
                FloatValue = CrewHealthDisplaySettings.PositionY,
                ToolTip = "Normalized vertical panel position"
            };
            BindImmediateApply(positionYInput);

            var hideAIRow = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.085f), root.RectTransform), isHorizontal: true, childAnchor: Anchor.CenterLeft)
            {
                Stretch = false
            };

            hideAICrewTickBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 1.0f), hideAIRow.RectTransform),
                "Hide AI crew")
            {
                Selected = CrewHealthDisplaySettings.HideAICrew,
                Enabled = !isEditorMode,
                ToolTip = isEditorMode ? "Disabled in Character Editor debug mode." : "Hide AI-controlled crew members."
            };
            hideAICrewTickBox.OnSelected = (tickBox) =>
            {
                if (refreshing) { return true; }
                ApplySettingsInputs(save: true);
                return true;
            };

            var hideDeadRow = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.085f), root.RectTransform), isHorizontal: true, childAnchor: Anchor.CenterLeft)
            {
                Stretch = false
            };

            hideDeadCrewTickBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 1.0f), hideDeadRow.RectTransform),
                "Hide dead crew")
            {
                Selected = CrewHealthDisplaySettings.HideDeadCrew,
                Enabled = !isEditorMode,
                ToolTip = isEditorMode ? "Disabled in Character Editor debug mode." : "Hide dead crew members and remembered death rows."
            };
            hideDeadCrewTickBox.OnSelected = (tickBox) =>
            {
                if (refreshing) { return true; }
                ApplySettingsInputs(save: true);
                return true;
            };

            var hideHealthyRow = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.085f), root.RectTransform), isHorizontal: true, childAnchor: Anchor.CenterLeft)
            {
                Stretch = false
            };

            hideHealthyCrewTickBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 1.0f), hideHealthyRow.RectTransform),
                "Hide crew above threshold")
            {
                Selected = CrewHealthDisplaySettings.HideHealthyCrew,
                Enabled = !isEditorMode,
                ToolTip = isEditorMode ? "Disabled in Character Editor debug mode." : "Hide crew members whose health percent is above the threshold."
            };
            hideHealthyCrewTickBox.OnSelected = (tickBox) =>
            {
                if (refreshing) { return true; }
                ApplySettingsInputs(save: true);
                return true;
            };

            var alwaysShowSelfRow = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.085f), root.RectTransform), isHorizontal: true, childAnchor: Anchor.CenterLeft)
            {
                Stretch = false
            };

            alwaysShowSelfTickBox = new GUITickBox(new RectTransform(new Vector2(1.0f, 1.0f), alwaysShowSelfRow.RectTransform),
                "Always show self")
            {
                Selected = CrewHealthDisplaySettings.AlwaysShowSelf,
                Enabled = !isEditorMode,
                ToolTip = isEditorMode ? "Disabled in Character Editor debug mode." : "Keep showing your own health even when healthy crew are hidden."
            };
            alwaysShowSelfTickBox.OnSelected = (tickBox) =>
            {
                if (refreshing) { return true; }
                ApplySettingsInputs(save: true);
                return true;
            };

            var thresholdRow = new GUILayoutGroup(new RectTransform(new Vector2(1.0f, 0.085f), root.RectTransform), isHorizontal: true, childAnchor: Anchor.CenterLeft)
            {
                Stretch = true,
                RelativeSpacing = 0.025f
            };

            new GUITextBlock(new RectTransform(new Vector2(0.43f, 1.0f), thresholdRow.RectTransform),
                "Healthy threshold (%)", textAlignment: Alignment.CenterLeft)
            {
                CanBeFocused = false
            };

            hideHealthyThresholdInput = new GUINumberInput(
                new RectTransform(new Vector2(0.34f, 0.90f), thresholdRow.RectTransform),
                NumberType.Int,
                buttonVisibility: GUINumberInput.ButtonVisibility.ForceVisible)
            {
                Font = GUIStyle.SmallFont,
                MinValueInt = CrewHealthDisplaySettings.MinHideHealthyThresholdPercent,
                MaxValueInt = CrewHealthDisplaySettings.MaxHideHealthyThresholdPercent,
                ValueStep = 10,
                IntValue = CrewHealthDisplaySettings.HideHealthyThresholdPercent,
                Enabled = !isEditorMode,
                ToolTip = isEditorMode ? "Disabled in Character Editor debug mode." : "Percent unit. 50 means 50% health."
            };
            BindImmediateApply(hideHealthyThresholdInput);

            new GUIFrame(new RectTransform(new Vector2(1.0f, 0.03f), root.RectTransform), style: null)
            {
                CanBeFocused = false
            };

            var buttonRow = new GUILayoutGroup(new RectTransform(new Vector2(0.48f, 0.085f), root.RectTransform, Anchor.Center), isHorizontal: true, childAnchor: Anchor.CenterLeft)
            {
                Stretch = true,
                RelativeSpacing = 0.05f
            };

            new GUIButton(new RectTransform(new Vector2(0.475f, 0.88f), buttonRow.RectTransform), "Reset", style: "GUIButtonSmall")
            {
                OnClicked = (_, _) =>
                {
                    CrewHealthDisplaySettings.Reset(save: true);
                    RefreshSettingsControls();
                    return true;
                }
            };

            new GUIButton(new RectTransform(new Vector2(0.475f, 0.88f), buttonRow.RectTransform), "Close", style: "GUIButtonSmall")
            {
                OnClicked = (_, _) =>
                {
                    settingsBox?.Close();
                    settingsBox = null;
                    return true;
                }
            };
        }

        private static void ApplySettingsInputs(bool save)
        {
            if (refreshing) { return; }
            CrewHealthDisplaySettings.Apply(
                scaleInput?.FloatValue ?? CrewHealthDisplaySettings.DisplayScale,
                positionXInput?.FloatValue ?? CrewHealthDisplaySettings.PositionX,
                positionYInput?.FloatValue ?? CrewHealthDisplaySettings.PositionY,
                hideAICrewTickBox?.Selected ?? CrewHealthDisplaySettings.HideAICrew,
                hideDeadCrewTickBox?.Selected ?? CrewHealthDisplaySettings.HideDeadCrew,
                hideHealthyCrewTickBox?.Selected ?? CrewHealthDisplaySettings.HideHealthyCrew,
                alwaysShowSelfTickBox?.Selected ?? CrewHealthDisplaySettings.AlwaysShowSelf,
                hideHealthyThresholdInput?.IntValue ?? CrewHealthDisplaySettings.HideHealthyThresholdPercent,
                save);
            RefreshSettingsControls();
        }

        private static void RefreshSettingsControls()
        {
            refreshing = true;
            try
            {
                if (scaleValueText != null)
                {
                    scaleValueText.Text = CrewHealthDisplaySettings.GetScaleLabel();
                }
                if (scaleInput != null)
                {
                    scaleInput.FloatValue = CrewHealthDisplaySettings.DisplayScale;
                }
                if (positionXInput != null)
                {
                    positionXInput.FloatValue = CrewHealthDisplaySettings.PositionX;
                }
                if (positionYInput != null)
                {
                    positionYInput.FloatValue = CrewHealthDisplaySettings.PositionY;
                }
                if (hideAICrewTickBox != null)
                {
                    hideAICrewTickBox.Selected = CrewHealthDisplaySettings.HideAICrew;
                }
                if (hideDeadCrewTickBox != null)
                {
                    hideDeadCrewTickBox.Selected = CrewHealthDisplaySettings.HideDeadCrew;
                }
                if (hideHealthyCrewTickBox != null)
                {
                    hideHealthyCrewTickBox.Selected = CrewHealthDisplaySettings.HideHealthyCrew;
                }
                if (alwaysShowSelfTickBox != null)
                {
                    alwaysShowSelfTickBox.Selected = CrewHealthDisplaySettings.AlwaysShowSelf;
                }
                if (hideHealthyThresholdInput != null)
                {
                    hideHealthyThresholdInput.IntValue = CrewHealthDisplaySettings.HideHealthyThresholdPercent;
                }
            }
            finally
            {
                refreshing = false;
            }
        }

        private static void BindImmediateApply(GUINumberInput input)
        {
            if (input == null) { return; }
            input.OnValueChanged = _ =>
            {
                if (refreshing) { return; }
                ApplySettingsInputs(save: true);
            };
            input.OnValueEntered = _ =>
            {
                if (refreshing) { return; }
                ApplySettingsInputs(save: true);
            };
        }

        private static void UpdateSettingsButton(Rectangle settingsButtonRect)
        {
            if (GUI.Canvas == null) { return; }
            if (buttonContainer == null || buttonContainer.RectTransform.Parent == null)
            {
                buttonContainer = new GUIFrame(new RectTransform(Vector2.One, GUI.Canvas), style: null)
                {
                    CanBeFocused = false
                };
            }
            if (settingsButton == null)
            {
                settingsButton = new GUIButton(
                    new RectTransform(new Point(settingsButtonRect.Width, settingsButtonRect.Height), buttonContainer.RectTransform, Anchor.TopLeft),
                    text: string.Empty,
                    style: null)
                {
                    CanBeFocused = true,
                    Enabled = true,
                    Visible = true,
                    ToolTip = "Crew Health Display Settings",
                    OnClicked = (_, _) =>
                    {
                        ToggleSettingsWindow();
                        return true;
                    }
                };
                settingsButton.Frame.Color = Color.Transparent;
                settingsButton.Frame.HoverColor = Color.Transparent;
                settingsButton.Frame.PressedColor = Color.Transparent;
                settingsButton.Frame.CanBeFocused = false;
            }

            settingsButton.RectTransform.Resize(new Point(settingsButtonRect.Width, settingsButtonRect.Height));
            settingsButton.RectTransform.AbsoluteOffset = new Point(settingsButtonRect.X, settingsButtonRect.Y);
            settingsButton.Visible = !GUI.InputBlockingMenuOpen && !GUI.DisableHUD;
            settingsButton.Enabled = settingsButton.Visible;
        }

        private static bool IsCharacterEditorSelected()
        {
            return Screen.Selected?.GetType().FullName == "Barotrauma.CharacterEditor.CharacterEditorScreen";
        }

        private static void ClearSettingsReferences()
        {
            settingsBox = null;
            scaleValueText = null;
            scaleInput = null;
            positionXInput = null;
            positionYInput = null;
            hideHealthyThresholdInput = null;
            hideAICrewTickBox = null;
            hideDeadCrewTickBox = null;
            hideHealthyCrewTickBox = null;
            alwaysShowSelfTickBox = null;
            refreshing = false;
        }
    }
}
