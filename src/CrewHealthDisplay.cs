using Barotrauma;
using Barotrauma.Plugins;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace CrewHealthDisplayMod
{
    public class CrewHealthDisplay : IBarotraumaPlugin
    {
        private Harmony harmony;
        private static IDebugConsole console;

        public void Init()
        {
            console = PluginServiceProvider.GetService<IDebugConsole>();
            harmony = PluginServiceProvider.GetService<IHarmonyProvider>().GetHarmony();
        }

        internal static void Log(string message) => console?.NewMessage(message);

        public void OnContentLoaded()
        {
            try
            {
                CrewHealthDisplaySettings.LoadConfig();

                var hudDraw = AccessTools.Method(typeof(CharacterHUD), "Draw", new[] { typeof(SpriteBatch), typeof(Character), typeof(Camera) });
                if (hudDraw != null)
                {
                    harmony.Patch(
                        hudDraw,
                        postfix: new HarmonyMethod(typeof(CharacterHUDDrawPatch), nameof(CharacterHUDDrawPatch.Postfix)));
                }
                else
                {
                    Log("Could not find CharacterHUD.Draw(SpriteBatch, Character, Camera). The in-game health panel is disabled.");
                }

                var hotkeyPostfix = new HarmonyMethod(typeof(CrewHealthDisplayUpdatePatch), nameof(CrewHealthDisplayUpdatePatch.Postfix));
                var gameScreenUpdate = AccessTools.Method(typeof(GameScreen), nameof(GameScreen.Update), new[] { typeof(double) });
                if (gameScreenUpdate != null)
                {
                    harmony.Patch(gameScreenUpdate, postfix: hotkeyPostfix);
                }
                else
                {
                    Log("Could not find GameScreen.Update(double). Alt+U is unavailable during gameplay.");
                }

                var characterRemove = AccessTools.Method(typeof(Character), nameof(Character.Remove));
                if (characterRemove != null)
                {
                    harmony.Patch(
                        characterRemove,
                        prefix: new HarmonyMethod(typeof(CrewHealthCharacterRemovePatch), nameof(CrewHealthCharacterRemovePatch.Prefix)));
                }
                else
                {
                    Log("Could not find Character.Remove. Removed crew members will not be tracked.");
                }

                var startRound = AccessTools.Method(
                    typeof(GameSession),
                    nameof(GameSession.StartRound),
                    new[] { typeof(LevelData), typeof(bool) });
                if (startRound != null)
                {
                    harmony.Patch(
                        startRound,
                        postfix: new HarmonyMethod(typeof(CrewHealthRoundLifecyclePatch), nameof(CrewHealthRoundLifecyclePatch.StartRoundPostfix)));
                }
                else
                {
                    Log("Could not find GameSession.StartRound(LevelData, bool). The panel may retain crew data from the previous round.");
                }

                Type editorType = AccessTools.TypeByName("Barotrauma.CharacterEditor.CharacterEditorScreen");
                if (editorType != null)
                {
                    var editorDraw = AccessTools.Method(
                        editorType,
                        "Draw",
                        new[] { typeof(double), typeof(GraphicsDevice), typeof(SpriteBatch) });

                    if (editorDraw != null)
                    {
                        harmony.Patch(
                            editorDraw,
                            postfix: new HarmonyMethod(typeof(CharacterHUDDrawPatch), nameof(CharacterHUDDrawPatch.EditorPostfix)));
                    }
                    else
                    {
                        Log("Could not find CharacterEditorScreen.Draw(double, GraphicsDevice, SpriteBatch). The editor health panel is disabled.");
                    }

                    var editorUpdate = AccessTools.DeclaredMethod(editorType, nameof(GameScreen.Update), new[] { typeof(double) });
                    if (editorUpdate != null)
                    {
                        harmony.Patch(editorUpdate, postfix: hotkeyPostfix);
                    }
                    else
                    {
                        Log("Could not find CharacterEditorScreen.Update(double). Alt+U is unavailable in the character editor.");
                    }
                }
                else
                {
                    Log("Could not find CharacterEditorScreen. The editor health panel is disabled.");
                }

                Log("Crew health display loaded.");
            }
            catch (Exception ex)
            {
                harmony.UnpatchSelf();
                console.ThrowError("Could not install the crew health display patches.", ex);
            }
        }

        public void Dispose()
        {
            try
            {
                CharacterHUDDrawPatch.DisposeUi();
            }
            finally
            {
                console = null;
            }
        }
    }

    public static class CrewHealthDisplayUpdatePatch
    {
        public static void Postfix(double deltaTime)
        {
            CharacterHUDDrawPatch.UpdateHotkey();
        }
    }

    public static class CrewHealthRoundLifecyclePatch
    {
        public static void StartRoundPostfix()
        {
            CrewHealthRemovedCrewMemory.ClearRoundSnapshots();
            CrewHealthCharacterFilter.ClearRoundState();
        }
    }

    [HarmonyPatch(typeof(CharacterHUD), "Draw")]
    public static class CharacterHUDDrawPatch
    {
        private const int PanelWidth = 260;
        private const int Margin = 18;
        private const int Padding = 10;
        private const int HeaderHeight = 20;
        private const int RowHeight = 24;
        private const int BarHeight = 7;
        private const int MaxRows = 50;
        private static bool loggedFirstDraw;
        private static bool panelVisible = true;
        private static readonly List<CrewHealthRemovedCrewMemory.RemovedCrewSnapshot> removedCrewRows = new List<CrewHealthRemovedCrewMemory.RemovedCrewSnapshot>();
        public static void Postfix(SpriteBatch spriteBatch, Character character, Camera cam)
        {
            try
            {
                if (spriteBatch == null) { return; }
                if (character == null) { return; }
                if (cam == null) { return; }
                if (GUI.DisableHUD) { return; }
                if (Character.CharacterList == null || Character.CharacterList.Count == 0) { return; }
                if (!panelVisible)
                {
                    CrewHealthSettingsWindow.HideInteractiveUi();
                    CrewHealthSettingsWindow.CleanupClosedSettingsBox();
                    return;
                }

                if (!loggedFirstDraw)
                {
                    loggedFirstDraw = true;
                    CrewHealthDisplay.Log($"HUD draw reached. CharacterList.Count={Character.CharacterList.Count}.");
                }

                DrawCrewHealthPanel(spriteBatch, isEditorMode: false);
                CrewHealthSettingsWindow.UpdateGuiButtons();
                CrewHealthSettingsWindow.CleanupClosedSettingsBox();
            }
            catch (Exception ex)
            {
                CrewHealthDisplay.Log($"HUD health draw failed. {ex}");
            }
        }

        public static void EditorPostfix(double deltaTime, GraphicsDevice graphics, SpriteBatch spriteBatch)
        {
            bool spriteBatchStarted = false;
            try
            {
                if (graphics == null) { return; }
                if (spriteBatch == null) { return; }
                if (GUI.DisableHUD) { return; }
                if (Character.CharacterList == null || Character.CharacterList.Count == 0) { return; }
                if (!panelVisible)
                {
                    CrewHealthSettingsWindow.HideInteractiveUi();
                    CrewHealthSettingsWindow.CleanupClosedSettingsBox();
                    return;
                }

                spriteBatch.Begin(
                    SpriteSortMode.Deferred,
                    samplerState: GUI.SamplerState,
                    rasterizerState: GameMain.ScissorTestEnable);
                spriteBatchStarted = true;

                DrawCrewHealthPanel(spriteBatch, isEditorMode: true);

                CrewHealthSettingsWindow.UpdateGuiButtons();
                CrewHealthSettingsWindow.CleanupClosedSettingsBox();
            }
            catch (Exception ex)
            {
                CrewHealthDisplay.Log($"Editor health draw failed. {ex}");
            }
            finally
            {
                if (spriteBatchStarted)
                {
                    try
                    {
                        spriteBatch.End();
                    }
                    catch (Exception endEx)
                    {
                        CrewHealthDisplay.Log($"Editor health SpriteBatch.End failed. {endEx.Message}");
                    }
                }
            }
        }

        public static void UpdateHotkey()
        {
            try
            {
                if (PlayerInput.IsAltDown() && PlayerInput.KeyHit(Keys.U))
                {
                    panelVisible = !panelVisible;
                    if (!panelVisible)
                    {
                        CrewHealthSettingsWindow.HideInteractiveUi();
                    }
                    CrewHealthDisplay.Log($"Alt+U toggle: {(panelVisible ? "shown" : "hidden")}.");
                }
            }
            catch (Exception ex)
            {
                CrewHealthDisplay.Log($"Hotkey update failed safely. {ex.Message}");
            }
        }

        public static void DisposeUi()
        {
            CrewHealthSettingsWindow.DisposeUi();
        }

        private static void DrawCrewHealthPanel(SpriteBatch spriteBatch, bool isEditorMode)
        {
            CrewHealthRemovedCrewMemory.UpdateFromCharacterList(isEditorMode);
            removedCrewRows.Clear();
            if (!isEditorMode && !CrewHealthDisplaySettings.HideDeadCrew)
            {
                CrewHealthRemovedCrewMemory.GetDrawableSnapshots(removedCrewRows, CrewHealthDisplaySettings.HideAICrew);
            }

            float scale = CrewHealthDisplaySettings.DisplayScale;
            int panelWidth = ScaleInt(PanelWidth, scale);
            int margin = ScaleInt(Margin, scale);
            int padding = ScaleInt(Padding, scale);
            int headerHeight = ScaleInt(HeaderHeight, scale);
            int rowHeight = ScaleInt(RowHeight, scale);
            int barHeight = Math.Max(4, ScaleInt(BarHeight, scale));
            int settingsButtonWidth = ScaleInt(38, scale);
            int settingsButtonHeight = ScaleInt(17, scale);

            int crewCandidateCount = CountCrewCandidates(isEditorMode);
            int rowCount = CountDrawableCharacters(isEditorMode) + removedCrewRows.Count;
            int visibleRows = Math.Min(rowCount, MaxRows);
            int panelHeight = padding * 2 + headerHeight + Math.Max(visibleRows, 1) * rowHeight;
            Point panelPos = GetPanelPosition(panelWidth, panelHeight, margin);
            Rectangle panelRect = new Rectangle(panelPos.X, panelPos.Y, panelWidth, panelHeight);

            GUI.DrawRectangle(spriteBatch, panelRect, Color.Black * 0.68f, isFilled: true);
            GUI.DrawRectangle(spriteBatch, panelRect, Color.White * 0.35f, isFilled: false, thickness: 1.0f);

            Vector2 headerPos = new Vector2(panelRect.X + padding, panelRect.Y + padding - scale);
            GUI.DrawString(spriteBatch, headerPos, isEditorMode ? "Editor Health" : "Crew Health", Color.White, Color.Black * 0.45f, Math.Max(1, ScaleInt(1, scale)), GetFont(scale), ForceUpperCase.No);

            Rectangle settingsButtonRect = new Rectangle(
                panelRect.Right - padding - settingsButtonWidth,
                panelRect.Y + Math.Max(2, padding / 2),
                settingsButtonWidth,
                settingsButtonHeight);
            bool mouseOnSettings = settingsButtonRect.Contains(PlayerInput.MousePosition);
            GUI.DrawRectangle(spriteBatch, settingsButtonRect, (mouseOnSettings ? new Color(65, 105, 125) : new Color(32, 48, 56)) * 0.92f, isFilled: true);
            GUI.DrawRectangle(spriteBatch, settingsButtonRect, Color.White * (mouseOnSettings ? 0.75f : 0.35f), isFilled: false);
            DrawCenteredString(spriteBatch, settingsButtonRect, "SET", Color.White, scale);
            CrewHealthSettingsWindow.Update(settingsButtonRect);

            if (rowCount <= 0)
            {
                string emptyText = isEditorMode ? $"No drawable characters ({crewCandidateCount} characters)" : $"No visible crew ({crewCandidateCount} crew)";
                GUI.DrawString(spriteBatch, new Vector2(panelRect.X + padding, panelRect.Y + padding + headerHeight), emptyText, Color.LightGray, null, 0, GetFont(scale), ForceUpperCase.No);
                return;
            }

            int drawn = 0;
            for (int i = 0; i < Character.CharacterList.Count && drawn < MaxRows; i++)
            {
                Character target = Character.CharacterList[i];
                if (!ShouldDrawCharacter(target, isEditorMode)) { continue; }

                int rowY = panelRect.Y + padding + headerHeight + drawn * rowHeight;
                DrawCharacterRow(spriteBatch, target, panelRect.X + padding, rowY, panelWidth - padding * 2, rowHeight, barHeight, scale);
                drawn++;
            }
            for (int i = 0; i < removedCrewRows.Count && drawn < MaxRows; i++)
            {
                int rowY = panelRect.Y + padding + headerHeight + drawn * rowHeight;
                DrawSnapshotRow(spriteBatch, removedCrewRows[i], panelRect.X + padding, rowY, panelWidth - padding * 2, rowHeight, barHeight, scale);
                drawn++;
            }
        }

        private static Point GetPanelPosition(int panelWidth, int panelHeight, int margin)
        {
            int screenWidth = Math.Max(1, GameMain.GraphicsWidth);
            int screenHeight = Math.Max(1, GameMain.GraphicsHeight);
            int minX = Math.Max(0, margin);
            int minY = Math.Max(0, margin);
            int maxX = Math.Max(minX, screenWidth - panelWidth - margin);
            int maxY = Math.Max(minY, screenHeight - panelHeight - margin);
            int x = (int)Math.Round(MathHelper.Lerp(minX, maxX, CrewHealthDisplaySettings.PositionX));
            int y = (int)Math.Round(MathHelper.Lerp(minY, maxY, CrewHealthDisplaySettings.PositionY));
            return new Point(ClampInt(x, minX, maxX), ClampInt(y, minY, maxY));
        }

        private static void DrawCenteredString(SpriteBatch spriteBatch, Rectangle rect, string text, Color color, float scale)
        {
            Vector2 size = MeasureText(text, scale);
            Vector2 pos = new Vector2(rect.Center.X - size.X / 2.0f, rect.Center.Y - size.Y / 2.0f);
            GUI.DrawString(spriteBatch, pos, text, color, null, 0, GetFont(scale), ForceUpperCase.No);
        }

        private static int ScaleInt(int value, float scale)
        {
            return Math.Max(1, (int)Math.Round(value * scale));
        }

        private static int ClampInt(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }

        private static int CountDrawableCharacters(bool isEditorMode)
        {
            return CrewHealthCharacterFilter.CountDrawableCharacters(
                isEditorMode,
                CrewHealthDisplaySettings.HideAICrew,
                CrewHealthDisplaySettings.HideDeadCrew,
                CrewHealthDisplaySettings.HideHealthyCrew,
                CrewHealthDisplaySettings.AlwaysShowSelf,
                CrewHealthDisplaySettings.HideHealthyThresholdPercent);
        }

        private static int CountCrewCandidates(bool isEditorMode)
        {
            return CrewHealthCharacterFilter.CountCandidates(isEditorMode);
        }

        private static bool ShouldDrawCharacter(Character character, bool isEditorMode)
        {
            return CrewHealthCharacterFilter.ShouldDrawCharacter(
                character,
                isEditorMode,
                CrewHealthDisplaySettings.HideAICrew,
                CrewHealthDisplaySettings.HideDeadCrew,
                CrewHealthDisplaySettings.HideHealthyCrew,
                CrewHealthDisplaySettings.AlwaysShowSelf,
                CrewHealthDisplaySettings.HideHealthyThresholdPercent);
        }

        private static void DrawCharacterRow(SpriteBatch spriteBatch, Character target, int x, int y, int width, int rowHeight, int barHeight, float scale)
        {
            bool isAwaitingInitialSync = CrewHealthCharacterFilter.IsAwaitingInitialSync(target);
            bool isDistant = CrewHealthCharacterFilter.IsDistantCrewMember(target);
            float maxHealth = Math.Max(target.MaxHealth, 1.0f);
            float rawHealth = target.Health;
            float health = MathHelper.Clamp(rawHealth, 0.0f, maxHealth);
            float healthRatio = MathHelper.Clamp(health / maxHealth, 0.0f, 1.0f);
            float minVitality = Math.Min(target.CharacterHealth.MinVitality, -1.0f);
            float downedRatio = MathHelper.Clamp((rawHealth - minVitality) / -minVitality, 0.0f, 1.0f);
            bool isDowned = !target.IsDead && !isDistant && !isAwaitingInitialSync && rawHealth <= 0.0f;

            string name = CrewHealthCharacterText.GetDisplayName(target);
            name = TruncateToWidth(name, width - ScaleInt(92, scale), scale);

            Color healthColor = isAwaitingInitialSync ? new Color(175, 175, 150) : isDistant ? new Color(115, 170, 210) : GetHealthColor(healthRatio, target.IsDead);
            Color textColor = target == Character.Controlled ? Color.Cyan : Color.White;
            if (isAwaitingInitialSync)
            {
                textColor = new Color(215, 215, 185);
            }
            else if (isDistant)
            {
                textColor = new Color(170, 210, 235);
            }
            if (isDowned)
            {
                textColor = new Color(255, 155, 155);
            }
            if (target.IsDead)
            {
                textColor = Color.White;
            }

            if (target.IsDead)
            {
                GUI.DrawRectangle(spriteBatch, new Rectangle(x - ScaleInt(4, scale), y - 1, width + ScaleInt(8, scale), rowHeight - 1), new Color(95, 10, 10) * 0.82f, isFilled: true);
            }
            else if (isAwaitingInitialSync)
            {
                GUI.DrawRectangle(spriteBatch, new Rectangle(x - ScaleInt(4, scale), y - 1, width + ScaleInt(8, scale), rowHeight - 1), new Color(58, 54, 36) * 0.70f, isFilled: true);
            }
            else if (isDistant)
            {
                GUI.DrawRectangle(spriteBatch, new Rectangle(x - ScaleInt(4, scale), y - 1, width + ScaleInt(8, scale), rowHeight - 1), new Color(18, 46, 66) * 0.70f, isFilled: true);
            }
            else if (isDowned)
            {
                GUI.DrawRectangle(spriteBatch, new Rectangle(x - ScaleInt(4, scale), y - 1, width + ScaleInt(8, scale), rowHeight - 1), new Color(80, 24, 18) * 0.55f, isFilled: true);
            }

            GUI.DrawString(spriteBatch, new Vector2(x, y), name, textColor, null, 0, GetFont(scale), ForceUpperCase.No);

            string healthText = target.IsDead ? "DEAD" : isAwaitingInitialSync ? "LOADING" : isDistant ? "DISTANT" : isDowned ? $"DOWN {Math.Ceiling(downedRatio * 100.0f):0}%" : $"{Math.Ceiling(health):0}/{Math.Ceiling(maxHealth):0}";
            Vector2 healthTextSize = MeasureText(healthText, scale);
            Vector2 healthTextPos = new Vector2(x + width - healthTextSize.X, y);
            if (target.IsDead || isDistant || isAwaitingInitialSync)
            {
                Rectangle deadTagRect = new Rectangle(
                    (int)healthTextPos.X - ScaleInt(4, scale),
                    y,
                    (int)healthTextSize.X + ScaleInt(8, scale),
                    ScaleInt(16, scale));
                Color tagColor = target.IsDead ? new Color(190, 20, 20) : isAwaitingInitialSync ? new Color(118, 108, 62) : new Color(35, 92, 126);
                GUI.DrawRectangle(spriteBatch, deadTagRect, tagColor, isFilled: true);
                GUI.DrawRectangle(spriteBatch, deadTagRect, Color.White * 0.45f, isFilled: false);
            }
            GUI.DrawString(spriteBatch, healthTextPos, healthText, target.IsDead || isDistant || isAwaitingInitialSync ? Color.White : isDowned ? new Color(255, 82, 82) : healthColor, null, 0, GetFont(scale), ForceUpperCase.No);

            Rectangle barBack = new Rectangle(x, y + ScaleInt(17, scale), width, barHeight);
            float fillRatio = isAwaitingInitialSync ? 0.0f : isDowned ? downedRatio : healthRatio;
            Color fillColor = isDowned ? new Color(230, 45, 45) : healthColor;
            Rectangle barFill = new Rectangle(barBack.X + 1, barBack.Y + 1, (int)((barBack.Width - 2) * fillRatio), barBack.Height - 2);

            GUI.DrawRectangle(spriteBatch, barBack, Color.Black * 0.75f, isFilled: true);
            if (barFill.Width > 0)
            {
                GUI.DrawRectangle(spriteBatch, barFill, fillColor * 0.9f, isFilled: true);
            }
            GUI.DrawRectangle(spriteBatch, barBack, (target.IsDead || isDowned ? Color.Red : isAwaitingInitialSync ? new Color(175, 175, 150) : isDistant ? new Color(115, 170, 210) : Color.White) * 0.35f, isFilled: false);
        }

        private static void DrawSnapshotRow(SpriteBatch spriteBatch, CrewHealthRemovedCrewMemory.RemovedCrewSnapshot snapshot, int x, int y, int width, int rowHeight, int barHeight, float scale)
        {
            string name = TruncateToWidth(snapshot.Name, width - ScaleInt(92, scale), scale);
            GUI.DrawRectangle(spriteBatch, new Rectangle(x - ScaleInt(4, scale), y - 1, width + ScaleInt(8, scale), rowHeight - 1), new Color(95, 10, 10) * 0.82f, isFilled: true);
            GUI.DrawString(spriteBatch, new Vector2(x, y), name, Color.White, null, 0, GetFont(scale), ForceUpperCase.No);

            const string healthText = "DEAD";
            Vector2 healthTextSize = MeasureText(healthText, scale);
            Vector2 healthTextPos = new Vector2(x + width - healthTextSize.X, y);
            Rectangle deadTagRect = new Rectangle(
                (int)healthTextPos.X - ScaleInt(4, scale),
                y,
                (int)healthTextSize.X + ScaleInt(8, scale),
                ScaleInt(16, scale));
            GUI.DrawRectangle(spriteBatch, deadTagRect, new Color(190, 20, 20), isFilled: true);
            GUI.DrawRectangle(spriteBatch, deadTagRect, Color.White * 0.45f, isFilled: false);
            GUI.DrawString(spriteBatch, healthTextPos, healthText, Color.White, null, 0, GetFont(scale), ForceUpperCase.No);

            Rectangle barBack = new Rectangle(x, y + ScaleInt(17, scale), width, barHeight);
            GUI.DrawRectangle(spriteBatch, barBack, Color.Black * 0.75f, isFilled: true);
            GUI.DrawRectangle(spriteBatch, barBack, Color.Red * 0.35f, isFilled: false);
        }

        private static Vector2 MeasureText(string text, float scale)
        {
            return GetFont(scale).MeasureString(text);
        }

        private static GUIFont GetFont(float scale)
        {
            return scale >= 1.15f ? GUIStyle.Font : GUIStyle.SmallFont;
        }

        private static Color GetHealthColor(float ratio, bool isDead)
        {
            if (isDead) { return Color.Gray; }
            if (ratio <= 0.25f) { return new Color(245, 160, 64); }
            if (ratio <= 0.55f) { return new Color(235, 210, 82); }
            return new Color(95, 210, 126);
        }

        private static string TruncateToWidth(string text, int maxWidth, float scale)
        {
            if (string.IsNullOrEmpty(text)) { return string.Empty; }
            if (MeasureText(text, scale).X <= maxWidth) { return text; }

            const string suffix = "...";
            for (int length = text.Length - 1; length > 0; length--)
            {
                string candidate = text.Substring(0, length) + suffix;
                if (MeasureText(candidate, scale).X <= maxWidth)
                {
                    return candidate;
                }
            }
            return suffix;
        }
    }

}
