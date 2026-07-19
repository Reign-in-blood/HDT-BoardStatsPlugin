using HearthDb.Enums;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;
using Hearthstone_Deck_Tracker.Plugins;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BoardStatsPlugin
{
    public class Plugin : IPlugin
    {
        // ------------------------------------------------------------
        // Plugin information
        // ------------------------------------------------------------

        public string Name => "Battlegrounds Board Stats";

        public string Description =>
            "Displays live board totals and a summary of all units deployed during the previous combat.";

        public string ButtonText => "Show / hide";
        public string Author => "Benito";
        public Version Version => new Version(0, 2, 8);
        public MenuItem MenuItem => null;

        // ------------------------------------------------------------
        // Visual settings
        // ------------------------------------------------------------

        // All title and content blocks use the same width.
        private const double PanelWidth = 300;

        private const double CurrentPlayerOnlyHeight = 30;
        private const double CurrentCombatHeight = 52;
        private const double PreviousCombatContentHeight = 52;

        private const double TitlePanelHeight = 20;
        private const double TitleContentGap = 2;
        private const double PanelSectionGap = 6;
        private const double PanelTop = 12;

        // Horizontal center of both panels.
        // 0.20 = approximately one fifth from the left edge.
        private const double PanelCenterXRatio = 0.20;

        private const double CurrentNameColumnWidth = 0;
        private const double CurrentStatLabelWidth = 26;
        private const double CurrentMiddleGapWidth = 6;

        private const double SummaryNameColumnWidth = 0;
        private const double SummaryUnitsLabelWidth = 36;
        private const double SummaryUnitsValueWidth = 34;
        private const double SummaryStatLabelWidth = 26;
        private const double SummaryGapWidth = 5;

        private const double FontSizeValue = 12;
        private const double HeaderFontSize = 11;

        private static readonly Brush PlayerBrush =
            CreateFrozenBrush(Color.FromRgb(70, 220, 90));

        private static readonly Brush OpponentBrush =
            CreateFrozenBrush(Color.FromRgb(255, 70, 70));

        private static readonly Brush HeaderBrush =
            CreateFrozenBrush(Color.FromRgb(220, 220, 220));

        private static readonly Brush PanelBrush =
            CreateFrozenBrush(Color.FromArgb(225, 0, 0, 0));

        // ------------------------------------------------------------
        // Current-board overlay
        // ------------------------------------------------------------

        private Border _currentTitlePanel;
        private Border _currentPanel;

        private TextBlock _currentRecruitmentTitle;
        private StackPanel _currentCombatTitle;

        private Grid _currentPlayerRow;
        private Grid _currentOpponentRow;

        private TextBlock _currentPlayerAttackValue;
        private TextBlock _currentPlayerHealthValue;
        private TextBlock _currentOpponentAttackValue;
        private TextBlock _currentOpponentHealthValue;

        // ------------------------------------------------------------
        // Previous-combat overlay
        // ------------------------------------------------------------

        private Border _lastCombatTitlePanel;
        private Border _lastCombatPanel;

        private TextBlock _summaryPlayerUnitsValue;
        private TextBlock _summaryPlayerAttackValue;
        private TextBlock _summaryPlayerHealthValue;

        private TextBlock _summaryOpponentUnitsValue;
        private TextBlock _summaryOpponentAttackValue;
        private TextBlock _summaryOpponentHealthValue;

        // ------------------------------------------------------------
        // General state
        // ------------------------------------------------------------

        private bool _pluginEnabled = true;
        private bool _currentPanelVisible;
        private bool _lastCombatPanelVisible;

        private BoardTotals _lastCurrentPlayerTotals =
            new BoardTotals(-1, -1);

        private BoardTotals _lastCurrentOpponentTotals =
            new BoardTotals(-1, -1);

        private bool? _previousCombatPhase;

        // ------------------------------------------------------------
        // Combat tracking
        // ------------------------------------------------------------

        // Player units use a logical-unit tracker. Hearthstone may use one
        // entity ID in recruitment and another entity ID for the same unit
        // during combat. Both IDs are mapped to one logical unit.
        private readonly PlayerCombatTracker
            _combatPlayerTracker =
                new PlayerCombatTracker();

        private readonly CombatSideTracker
            _combatOpponentTracker =
                new CombatSideTracker();

        private readonly List<InitialUnitSnapshot>
            _latestRecruitmentPlayerUnits =
                new List<InitialUnitSnapshot>();

        private const bool EnableDiagnosticLog = true;

        private bool _combatTrackingActive;
        private bool _hasLastCombatSummary;

        private CombatSummary _lastCombatSummary =
            CombatSummary.Empty;

        // ------------------------------------------------------------
        // HDT plugin lifecycle
        // ------------------------------------------------------------

        public void OnLoad()
        {
            Core.OverlayCanvas.Dispatcher.Invoke(() =>
            {
                CreateOverlays();
                HideAllOverlays();
            });
        }

        public void OnUnload()
        {
            Core.OverlayCanvas.Dispatcher.Invoke(() =>
            {
                if (_currentTitlePanel != null)
                    Core.OverlayCanvas.Children.Remove(_currentTitlePanel);

                if (_currentPanel != null)
                    Core.OverlayCanvas.Children.Remove(_currentPanel);

                if (_lastCombatTitlePanel != null)
                    Core.OverlayCanvas.Children.Remove(
                        _lastCombatTitlePanel
                    );

                if (_lastCombatPanel != null)
                    Core.OverlayCanvas.Children.Remove(_lastCombatPanel);

                _currentTitlePanel = null;
                _currentPanel = null;
                _lastCombatTitlePanel = null;
                _lastCombatPanel = null;

                _currentRecruitmentTitle = null;
                _currentCombatTitle = null;

                _currentPlayerRow = null;
                _currentOpponentRow = null;

                _currentPanelVisible = false;
                _lastCombatPanelVisible = false;
            });

            ResetMatchTracking();
        }

        public void OnButtonPress()
        {
            _pluginEnabled = !_pluginEnabled;

            if (!_pluginEnabled)
                Core.OverlayCanvas.Dispatcher.Invoke(HideAllOverlays);
        }

        // HDT calls this method approximately every 100 ms.
        public void OnUpdate()
        {
            try
            {
                bool isActiveBattlegroundsMatch =
                    Core.Game.IsRunning
                    && !Core.Game.IsInMenu
                    && Core.Game.IsBattlegroundsMatch;

                bool shouldShow =
                    _pluginEnabled
                    && isActiveBattlegroundsMatch;

                if (!isActiveBattlegroundsMatch)
                {
                    if (_currentPanelVisible || _lastCombatPanelVisible)
                        Core.OverlayCanvas.Dispatcher.Invoke(
                            HideAllOverlays
                        );

                    ResetMatchTracking();
                    return;
                }

                if (!shouldShow)
                    return;

                bool isCombatPhase =
                    Core.Game.IsBattlegroundsCombatPhase;

                if (!isCombatPhase)
                    CapturePlayerRecruitmentBoard();

                HandleCombatTracking(isCombatPhase);

                BoardTotals currentPlayerTotals =
                    CalculateCurrentBoardTotals(
                        Core.Game.Player.Minions
                    );

                BoardTotals currentOpponentTotals =
                    isCombatPhase
                        ? CalculateCurrentBoardTotals(
                            Core.Game.Opponent.Minions
                        )
                        : new BoardTotals(0, 0);

                bool phaseChanged =
                    !_previousCombatPhase.HasValue
                    || _previousCombatPhase.Value != isCombatPhase;

                bool currentTotalsChanged =
                    currentPlayerTotals.Attack
                        != _lastCurrentPlayerTotals.Attack
                    || currentPlayerTotals.Health
                        != _lastCurrentPlayerTotals.Health
                    || (
                        isCombatPhase
                        && (
                            currentOpponentTotals.Attack
                                != _lastCurrentOpponentTotals.Attack
                            || currentOpponentTotals.Health
                                != _lastCurrentOpponentTotals.Health
                        )
                    )
                    || phaseChanged;

                if (
                    currentTotalsChanged
                    || !_currentPanelVisible
                    || (
                        !isCombatPhase
                        && _hasLastCombatSummary
                        && !_lastCombatPanelVisible
                    )
                    || (
                        isCombatPhase
                        && _lastCombatPanelVisible
                    )
                )
                {
                    _lastCurrentPlayerTotals =
                        currentPlayerTotals;

                    _lastCurrentOpponentTotals =
                        currentOpponentTotals;

                    Core.OverlayCanvas.Dispatcher.Invoke(() =>
                    {
                        CreateOverlays();

                        UpdateCurrentBoardOverlay(
                            currentPlayerTotals,
                            currentOpponentTotals,
                            isCombatPhase
                        );

                        UpdateLastCombatOverlay(
                            isCombatPhase
                        );

                        PositionOverlays();
                    });
                }

                _previousCombatPhase = isCombatPhase;
            }
            catch
            {
                // Data can be temporarily unavailable during animations
                // or transitions. The next update retries automatically.
            }
        }

        // ------------------------------------------------------------
        // Combat tracking
        // ------------------------------------------------------------

        private void HandleCombatTracking(bool isCombatPhase)
        {
            if (isCombatPhase)
            {
                if (!_combatTrackingActive)
                    StartCombatTracking();

                TrackPlayerCombatSide();

                TrackCombatSide(
                    Core.Game.Opponent.Id,
                    _combatOpponentTracker
                );

                return;
            }

            if (_combatTrackingActive)
                FinishCombatTracking();
        }

        private void CapturePlayerRecruitmentBoard()
        {
            _latestRecruitmentPlayerUnits.Clear();

            IEnumerable<Entity> minions =
                Core.Game.Player.Minions;

            if (minions == null)
                return;

            foreach (Entity minion in minions)
            {
                if (!IsTrackablePlayerMinion(minion))
                    continue;

                _latestRecruitmentPlayerUnits.Add(
                    InitialUnitSnapshot.FromEntity(minion)
                );
            }
        }

        private void StartCombatTracking()
        {
            _combatPlayerTracker.Clear();
            _combatOpponentTracker.Clear();

            _combatPlayerTracker.SeedInitialUnits(
                _latestRecruitmentPlayerUnits
            );

            WriteDiagnostic(
                "COMBAT START | initial player units="
                + _latestRecruitmentPlayerUnits.Count
            );

            foreach (
                InitialUnitSnapshot snapshot
                in _latestRecruitmentPlayerUnits
            )
            {
                WriteDiagnostic(
                    "INITIAL | id=" + snapshot.EntityId
                    + " | pos=" + snapshot.ZonePosition
                    + " | card=" + snapshot.CardId
                    + " | atk=" + snapshot.Attack
                    + " | hp=" + snapshot.MaximumHealth
                );
            }

            _combatTrackingActive = true;
        }

        private void FinishCombatTracking()
        {
            _lastCombatSummary = new CombatSummary(
                _combatPlayerTracker.CalculateTotals(),
                _combatOpponentTracker.CalculateTotals()
            );

            _hasLastCombatSummary =
                _lastCombatSummary.Player.Units > 0
                || _lastCombatSummary.Opponent.Units > 0;

            WriteDiagnostic(
                "COMBAT END | player units="
                + _lastCombatSummary.Player.Units
                + " | atk="
                + _lastCombatSummary.Player.Attack
                + " | hp="
                + _lastCombatSummary.Player.Health
                + " | opponent units="
                + _lastCombatSummary.Opponent.Units
            );

            _combatTrackingActive = false;
            _combatPlayerTracker.Clear();
            _combatOpponentTracker.Clear();
        }

        private void TrackPlayerCombatSide()
        {
            _combatPlayerTracker.Observe(
                GetTrackableCombatMinions(
                    Core.Game.Player.Id
                )
            );
        }

        private static bool IsTrackablePlayerMinion(
            Entity entity)
        {
            return entity != null
                && entity.Id > 0
                && entity.IsMinion
                && entity.IsInPlay
                && !string.IsNullOrEmpty(entity.CardId);
        }

        private static void TrackCombatSide(
            int controllerId,
            CombatSideTracker tracker)
        {
            TrackCombatEntities(
                GetTrackableCombatMinions(controllerId),
                tracker
            );
        }

        private static List<Entity> GetTrackableCombatMinions(
            int controllerId)
        {
            List<Entity> result =
                new List<Entity>();

            foreach (Entity entity in Core.Game.Entities.Values)
            {
                if (
                    IsTrackableCombatMinion(
                        entity,
                        controllerId
                    )
                )
                {
                    result.Add(entity);
                }
            }

            return result;
        }

        private static void TrackCombatEntities(
            IEnumerable<Entity> entities,
            CombatSideTracker tracker)
        {
            tracker.Observe(entities);
        }

        private static bool IsTrackableCombatMinion(
            Entity entity,
            int controllerId)
        {
            return entity != null
                && entity.Id > 0
                && entity.IsMinion
                && entity.IsInPlay
                && entity.IsControlledBy(controllerId)
                && !string.IsNullOrEmpty(entity.CardId);
        }

        private void ResetMatchTracking()
        {
            _previousCombatPhase = null;
            _combatTrackingActive = false;

            _combatPlayerTracker.Clear();
            _combatOpponentTracker.Clear();
            _latestRecruitmentPlayerUnits.Clear();

            _hasLastCombatSummary = false;
            _lastCombatSummary = CombatSummary.Empty;

            _lastCurrentPlayerTotals =
                new BoardTotals(-1, -1);

            _lastCurrentOpponentTotals =
                new BoardTotals(-1, -1);
        }

        private static void WriteDiagnostic(string message)
        {
            if (!EnableDiagnosticLog)
                return;

            try
            {
                string assemblyPath =
                    typeof(Plugin).Assembly.Location;

                string directory =
                    Path.GetDirectoryName(assemblyPath);

                if (string.IsNullOrEmpty(directory))
                    return;

                string path = Path.Combine(
                    directory,
                    "BoardStatsPlugin_debug.log"
                );

                File.AppendAllText(
                    path,
                    DateTime.Now.ToString(
                        "yyyy-MM-dd HH:mm:ss.fff",
                        CultureInfo.InvariantCulture
                    )
                    + " | " + message
                    + Environment.NewLine
                );
            }
            catch
            {
                // Diagnostic logging must never interrupt the plugin.
            }
        }

        // ------------------------------------------------------------
        // Current board calculation
        // ------------------------------------------------------------

        private static BoardTotals CalculateCurrentBoardTotals(
            IEnumerable<Entity> minions)
        {
            if (minions == null)
                return new BoardTotals(0, 0);

            long totalAttack = 0;
            long totalHealth = 0;

            foreach (Entity minion in minions)
            {
                if (minion == null || minion.Health <= 0)
                    continue;

                totalAttack += Math.Max(0, minion.Attack);
                totalHealth += Math.Max(0, minion.Health);
            }

            return new BoardTotals(
                totalAttack,
                totalHealth
            );
        }

        // ------------------------------------------------------------
        // Overlay updates
        // ------------------------------------------------------------

        private void UpdateCurrentBoardOverlay(
            BoardTotals playerTotals,
            BoardTotals opponentTotals,
            bool isCombatPhase)
        {
            SetDisplayedValue(
                _currentPlayerAttackValue,
                playerTotals.Attack
            );

            SetDisplayedValue(
                _currentPlayerHealthValue,
                playerTotals.Health
            );

            if (isCombatPhase)
            {
                SetDisplayedValue(
                    _currentOpponentAttackValue,
                    opponentTotals.Attack
                );

                SetDisplayedValue(
                    _currentOpponentHealthValue,
                    opponentTotals.Health
                );

                _currentRecruitmentTitle.Visibility =
                    Visibility.Collapsed;

                _currentCombatTitle.Visibility =
                    Visibility.Visible;

                _currentOpponentRow.Visibility =
                    Visibility.Visible;

                _currentPanel.Height =
                    CurrentCombatHeight;
            }
            else
            {
                _currentRecruitmentTitle.Visibility =
                    Visibility.Visible;

                _currentCombatTitle.Visibility =
                    Visibility.Collapsed;

                _currentOpponentRow.Visibility =
                    Visibility.Collapsed;

                _currentPanel.Height =
                    CurrentPlayerOnlyHeight;
            }

            _currentTitlePanel.Visibility =
                Visibility.Visible;

            _currentPanel.Visibility =
                Visibility.Visible;

            _currentPanelVisible = true;
        }

        private void UpdateLastCombatOverlay(
            bool isCombatPhase)
        {
            bool shouldShowSummary =
                !isCombatPhase
                && _hasLastCombatSummary;

            if (!shouldShowSummary)
            {
                _lastCombatTitlePanel.Visibility =
                    Visibility.Collapsed;

                _lastCombatPanel.Visibility =
                    Visibility.Collapsed;

                _lastCombatPanelVisible = false;
                return;
            }

            SetDisplayedValue(
                _summaryPlayerUnitsValue,
                _lastCombatSummary.Player.Units
            );

            SetDisplayedValue(
                _summaryPlayerAttackValue,
                _lastCombatSummary.Player.Attack
            );

            SetDisplayedValue(
                _summaryPlayerHealthValue,
                _lastCombatSummary.Player.Health
            );

            SetDisplayedValue(
                _summaryOpponentUnitsValue,
                _lastCombatSummary.Opponent.Units
            );

            SetDisplayedValue(
                _summaryOpponentAttackValue,
                _lastCombatSummary.Opponent.Attack
            );

            SetDisplayedValue(
                _summaryOpponentHealthValue,
                _lastCombatSummary.Opponent.Health
            );

            _lastCombatTitlePanel.Visibility =
                Visibility.Visible;

            _lastCombatPanel.Visibility =
                Visibility.Visible;

            _lastCombatPanelVisible = true;
        }

        // ------------------------------------------------------------
        // Overlay creation
        // ------------------------------------------------------------

        private void CreateOverlays()
        {
            CreateCurrentTitleOverlay();
            CreateCurrentBoardOverlay();
            CreateLastCombatTitleOverlay();
            CreateLastCombatOverlay();
        }

        private void CreateCurrentTitleOverlay()
        {
            if (_currentTitlePanel != null)
                return;

            _currentRecruitmentTitle =
                CreateTitleTextBlock(
                    "BOARD STATS",
                    PlayerBrush
                );

            TextBlock playerTitle =
                CreateTitleTextBlock(
                    "PLAYER",
                    PlayerBrush
                );

            TextBlock versusTitle =
                CreateTitleTextBlock(
                    "  VS  ",
                    HeaderBrush
                );

            TextBlock opponentTitle =
                CreateTitleTextBlock(
                    "OPPONENT",
                    OpponentBrush
                );

            _currentCombatTitle = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };

            _currentCombatTitle.Children.Add(playerTitle);
            _currentCombatTitle.Children.Add(versusTitle);
            _currentCombatTitle.Children.Add(opponentTitle);

            Grid titleContent = new Grid();

            titleContent.Children.Add(
                _currentRecruitmentTitle
            );

            titleContent.Children.Add(
                _currentCombatTitle
            );

            _currentTitlePanel = CreateTitlePanel(
                titleContent
            );

            Core.OverlayCanvas.Children.Add(
                _currentTitlePanel
            );
        }

        private void CreateCurrentBoardOverlay()
        {
            if (_currentPanel != null)
                return;

            _currentPlayerRow = CreateCurrentStatsRow(
                "",
                PlayerBrush,
                out _currentPlayerAttackValue,
                out _currentPlayerHealthValue
            );

            _currentOpponentRow = CreateCurrentStatsRow(
                "",
                OpponentBrush,
                out _currentOpponentAttackValue,
                out _currentOpponentHealthValue
            );

            Grid rowsGrid = new Grid
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

            rowsGrid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                }
            );

            rowsGrid.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                }
            );

            Grid.SetRow(_currentPlayerRow, 0);
            Grid.SetRow(_currentOpponentRow, 1);

            rowsGrid.Children.Add(_currentPlayerRow);
            rowsGrid.Children.Add(_currentOpponentRow);

            _currentPanel = new Border
            {
                Width = PanelWidth,
                Height = CurrentPlayerOnlyHeight,
                Background = PanelBrush,
                Padding = new Thickness(8, 3, 8, 3),
                CornerRadius = new CornerRadius(3),
                Child = rowsGrid,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };

            Core.OverlayCanvas.Children.Add(_currentPanel);
        }

        private void CreateLastCombatTitleOverlay()
        {
            if (_lastCombatTitlePanel != null)
                return;

            TextBlock title =
                CreateTitleTextBlock(
                    "PREVIOUS COMBAT",
                    HeaderBrush
                );

            _lastCombatTitlePanel =
                CreateTitlePanel(title);

            Core.OverlayCanvas.Children.Add(
                _lastCombatTitlePanel
            );
        }

        private void CreateLastCombatOverlay()
        {
            if (_lastCombatPanel != null)
                return;

            Grid playerRow = CreateSummaryStatsRow(
                "",
                PlayerBrush,
                out _summaryPlayerUnitsValue,
                out _summaryPlayerAttackValue,
                out _summaryPlayerHealthValue
            );

            Grid opponentRow = CreateSummaryStatsRow(
                "",
                OpponentBrush,
                out _summaryOpponentUnitsValue,
                out _summaryOpponentAttackValue,
                out _summaryOpponentHealthValue
            );

            Grid content = new Grid
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

            content.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = new GridLength(22)
                }
            );

            content.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = new GridLength(22)
                }
            );

            Grid.SetRow(playerRow, 0);
            Grid.SetRow(opponentRow, 1);

            content.Children.Add(playerRow);
            content.Children.Add(opponentRow);

            _lastCombatPanel = new Border
            {
                Width = PanelWidth,
                Height = PreviousCombatContentHeight,
                Background = PanelBrush,
                Padding = new Thickness(8, 3, 8, 3),
                CornerRadius = new CornerRadius(3),
                Child = content,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };

            Core.OverlayCanvas.Children.Add(
                _lastCombatPanel
            );
        }

        private static Grid CreateCurrentStatsRow(
            string name,
            Brush foreground,
            out TextBlock attackValue,
            out TextBlock healthValue)
        {
            Grid row = new Grid
            {
                Height = 22,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

            // NAME | VALUE | ATK | GAP | VALUE | HP
            row.ColumnDefinitions.Add(
                CreateFixedColumn(
                    CurrentNameColumnWidth
                )
            );

            row.ColumnDefinitions.Add(
                CreateStarColumn()
            );

            row.ColumnDefinitions.Add(
                CreateFixedColumn(
                    CurrentStatLabelWidth
                )
            );

            row.ColumnDefinitions.Add(
                CreateFixedColumn(
                    CurrentMiddleGapWidth
                )
            );

            row.ColumnDefinitions.Add(
                CreateStarColumn()
            );

            row.ColumnDefinitions.Add(
                CreateFixedColumn(
                    CurrentStatLabelWidth
                )
            );

            TextBlock nameText =
                CreateLabelTextBlock(
                    name,
                    foreground,
                    TextAlignment.Left
                );

            attackValue =
                CreateValueTextBlock(foreground);

            TextBlock attackLabel =
                CreateLabelTextBlock(
                    "ATK",
                    foreground,
                    TextAlignment.Left
                );

            healthValue =
                CreateValueTextBlock(foreground);

            TextBlock healthLabel =
                CreateLabelTextBlock(
                    "HP",
                    foreground,
                    TextAlignment.Left
                );

            AddToColumn(row, nameText, 0);
            AddToColumn(row, attackValue, 1);
            AddToColumn(row, attackLabel, 2);
            AddToColumn(row, healthValue, 4);
            AddToColumn(row, healthLabel, 5);

            return row;
        }

        private static Grid CreateSummaryStatsRow(
            string name,
            Brush foreground,
            out TextBlock unitsValue,
            out TextBlock attackValue,
            out TextBlock healthValue)
        {
            Grid row = new Grid
            {
                Height = 22,
                VerticalAlignment =
                    VerticalAlignment.Center
            };

            // NAME | COUNT | UNITS | GAP | VALUE | ATK |
            // GAP | VALUE | HP
            row.ColumnDefinitions.Add(
                CreateFixedColumn(
                    SummaryNameColumnWidth
                )
            );

            row.ColumnDefinitions.Add(
                CreateFixedColumn(
                    SummaryUnitsValueWidth
                )
            );

            row.ColumnDefinitions.Add(
                CreateFixedColumn(
                    SummaryUnitsLabelWidth
                )
            );

            row.ColumnDefinitions.Add(
                CreateFixedColumn(
                    SummaryGapWidth
                )
            );

            row.ColumnDefinitions.Add(
                CreateStarColumn()
            );

            row.ColumnDefinitions.Add(
                CreateFixedColumn(
                    SummaryStatLabelWidth
                )
            );

            row.ColumnDefinitions.Add(
                CreateFixedColumn(
                    SummaryGapWidth
                )
            );

            row.ColumnDefinitions.Add(
                CreateStarColumn()
            );

            row.ColumnDefinitions.Add(
                CreateFixedColumn(
                    SummaryStatLabelWidth
                )
            );

            TextBlock nameText =
                CreateLabelTextBlock(
                    name,
                    foreground,
                    TextAlignment.Left
                );

            unitsValue =
                CreateValueTextBlock(foreground);

            TextBlock unitsLabel =
                CreateLabelTextBlock(
                    "UNITS",
                    foreground,
                    TextAlignment.Left
                );

            attackValue =
                CreateValueTextBlock(foreground);

            TextBlock attackLabel =
                CreateLabelTextBlock(
                    "ATK",
                    foreground,
                    TextAlignment.Left
                );

            healthValue =
                CreateValueTextBlock(foreground);

            TextBlock healthLabel =
                CreateLabelTextBlock(
                    "HP",
                    foreground,
                    TextAlignment.Left
                );

            AddToColumn(row, nameText, 0);
            AddToColumn(row, unitsValue, 1);
            AddToColumn(row, unitsLabel, 2);
            AddToColumn(row, attackValue, 4);
            AddToColumn(row, attackLabel, 5);
            AddToColumn(row, healthValue, 7);
            AddToColumn(row, healthLabel, 8);

            return row;
        }

        private static Border CreateTitlePanel(
            UIElement child)
        {
            return new Border
            {
                Width = PanelWidth,
                Height = TitlePanelHeight,
                Background = PanelBrush,
                Padding = new Thickness(4, 1, 4, 1),
                CornerRadius = new CornerRadius(3),
                Child = child,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
        }

        private static TextBlock CreateTitleTextBlock(
            string text,
            Brush foreground)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = HeaderFontSize,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                VerticalAlignment =
                    VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                IsHitTestVisible = false
            };
        }

        private static ColumnDefinition CreateFixedColumn(
            double width)
        {
            return new ColumnDefinition
            {
                Width = new GridLength(width)
            };
        }

        private static ColumnDefinition CreateStarColumn()
        {
            return new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star
                )
            };
        }

        private static TextBlock CreateLabelTextBlock(
            string text,
            Brush foreground,
            TextAlignment alignment)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = foreground,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = FontSizeValue,
                FontWeight = FontWeights.Bold,
                TextAlignment = alignment,
                VerticalAlignment =
                    VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                IsHitTestVisible = false
            };
        }

        private static TextBlock CreateValueTextBlock(
            Brush foreground)
        {
            return new TextBlock
            {
                Text = "0",
                Foreground = foreground,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = FontSizeValue,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Right,

                // Fixed visual space before ATK, HP or UNITS.
                Margin = new Thickness(0, 0, 5, 0),

                VerticalAlignment =
                    VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                IsHitTestVisible = false
            };
        }

        private static void AddToColumn(
            Grid grid,
            UIElement element,
            int column)
        {
            Grid.SetColumn(element, column);
            grid.Children.Add(element);
        }

        private static void SetDisplayedValue(
            TextBlock textBlock,
            long value)
        {
            textBlock.Text =
                value.ToString(
                    CultureInfo.InvariantCulture
                );
        }

        // ------------------------------------------------------------
        // Positioning and visibility
        // ------------------------------------------------------------

        private void PositionOverlays()
        {
            double currentTitleTop =
                PanelTop;

            double currentContentTop =
                currentTitleTop
                + TitlePanelHeight
                + TitleContentGap;

            PositionPanel(
                _currentTitlePanel,
                PanelWidth,
                currentTitleTop
            );

            PositionPanel(
                _currentPanel,
                PanelWidth,
                currentContentTop
            );

            double previousTitleTop =
                currentContentTop
                + _currentPanel.Height
                + PanelSectionGap;

            double previousContentTop =
                previousTitleTop
                + TitlePanelHeight
                + TitleContentGap;

            PositionPanel(
                _lastCombatTitlePanel,
                PanelWidth,
                previousTitleTop
            );

            PositionPanel(
                _lastCombatPanel,
                PanelWidth,
                previousContentTop
            );
        }

        private static void PositionPanel(
            FrameworkElement panel,
            double panelWidth,
            double top)
        {
            if (panel == null)
                return;

            double overlayWidth =
                Core.OverlayCanvas.ActualWidth;

            double requestedLeft =
                overlayWidth * PanelCenterXRatio
                - panelWidth / 2;

            double maximumLeft =
                Math.Max(
                    0,
                    overlayWidth - panelWidth
                );

            double left =
                Math.Max(
                    0,
                    Math.Min(
                        requestedLeft,
                        maximumLeft
                    )
                );

            Canvas.SetLeft(panel, left);
            Canvas.SetTop(panel, top);
        }

        private void HideAllOverlays()
        {
            if (_currentTitlePanel != null)
                _currentTitlePanel.Visibility =
                    Visibility.Collapsed;

            if (_currentPanel != null)
                _currentPanel.Visibility =
                    Visibility.Collapsed;

            if (_lastCombatTitlePanel != null)
                _lastCombatTitlePanel.Visibility =
                    Visibility.Collapsed;

            if (_lastCombatPanel != null)
                _lastCombatPanel.Visibility =
                    Visibility.Collapsed;

            _currentPanelVisible = false;
            _lastCombatPanelVisible = false;
        }

        private static Brush CreateFrozenBrush(
            Color color)
        {
            SolidColorBrush brush =
                new SolidColorBrush(color);

            brush.Freeze();
            return brush;
        }

        // ------------------------------------------------------------
        // Data types
        // ------------------------------------------------------------

        private sealed class PlayerCombatTracker
        {
            private readonly List<LogicalCombatUnit>
                _units =
                    new List<LogicalCombatUnit>();

            private readonly Dictionary<int, LogicalCombatUnit>
                _unitsByEntityId =
                    new Dictionary<int, LogicalCombatUnit>();

            private readonly List<LogicalCombatUnit>
                _initialUnitsWaitingForCombatAlias =
                    new List<LogicalCombatUnit>();

            public void SeedInitialUnits(
                IEnumerable<InitialUnitSnapshot> snapshots)
            {
                Clear();

                if (snapshots == null)
                    return;

                foreach (
                    InitialUnitSnapshot snapshot
                    in snapshots
                )
                {
                    LogicalCombatUnit unit =
                        LogicalCombatUnit.FromInitialSnapshot(
                            snapshot
                        );

                    _units.Add(unit);
                    _initialUnitsWaitingForCombatAlias.Add(unit);

                    if (snapshot.EntityId > 0)
                    {
                        _unitsByEntityId[
                            snapshot.EntityId
                        ] = unit;
                    }
                }
            }

            public void Observe(
                IEnumerable<Entity> entities)
            {
                if (entities == null)
                    return;

                List<Entity> unknownEntities =
                    new List<Entity>();

                foreach (Entity entity in entities)
                {
                    if (entity == null || entity.Id <= 0)
                        continue;

                    LogicalCombatUnit knownUnit;

                    if (
                        _unitsByEntityId.TryGetValue(
                            entity.Id,
                            out knownUnit
                        )
                    )
                    {
                        knownUnit.PeakStats.Observe(entity);
                    }
                    else
                    {
                        unknownEntities.Add(entity);
                    }
                }

                // First match by both board position and card identity.
                MatchUnknownEntities(
                    unknownEntities,
                    requireSamePosition: true
                );

                // Fallback for copies whose position tag is temporarily
                // unavailable, but whose card identity is still reliable.
                MatchUnknownEntities(
                    unknownEntities,
                    requireSamePosition: false
                );

                // Every remaining entity is a real additional deployment.
                foreach (Entity entity in unknownEntities)
                {
                    LogicalCombatUnit summonedUnit =
                        LogicalCombatUnit.FromSummonedEntity(
                            entity
                        );

                    _units.Add(summonedUnit);
                    _unitsByEntityId[entity.Id] = summonedUnit;

                    WriteDiagnostic(
                        "PLAYER SUMMON | id=" + entity.Id
                        + " | pos=" + entity.ZonePosition
                        + " | card=" + entity.CardId
                        + " | originalCard="
                        + (entity.Info.OriginalCardId ?? "")
                        + " | creator="
                        + entity.Info.GetCreatorId()
                    );
                }
            }

            private void MatchUnknownEntities(
                List<Entity> unknownEntities,
                bool requireSamePosition)
            {
                for (
                    int entityIndex = unknownEntities.Count - 1;
                    entityIndex >= 0;
                    entityIndex--
                )
                {
                    Entity entity =
                        unknownEntities[entityIndex];

                    LogicalCombatUnit match =
                        FindMatchingInitialUnit(
                            entity,
                            requireSamePosition
                        );

                    if (match == null)
                        continue;

                    match.PeakStats.Observe(entity);
                    match.CombatAliasEntityId = entity.Id;

                    _unitsByEntityId[entity.Id] = match;
                    _initialUnitsWaitingForCombatAlias.Remove(
                        match
                    );

                    unknownEntities.RemoveAt(entityIndex);

                    WriteDiagnostic(
                        "PLAYER ALIAS | combatId=" + entity.Id
                        + " -> initialId="
                        + match.InitialEntityId
                        + " | pos=" + entity.ZonePosition
                        + " | card=" + entity.CardId
                        + " | originalCard="
                        + (entity.Info.OriginalCardId ?? "")
                    );
                }
            }

            private LogicalCombatUnit FindMatchingInitialUnit(
                Entity entity,
                bool requireSamePosition)
            {
                foreach (
                    LogicalCombatUnit unit
                    in _initialUnitsWaitingForCombatAlias
                )
                {
                    if (
                        requireSamePosition
                        && unit.InitialZonePosition > 0
                        && entity.ZonePosition > 0
                        && unit.InitialZonePosition
                            != entity.ZonePosition
                    )
                    {
                        continue;
                    }

                    bool sameCard =
                        string.Equals(
                            unit.InitialCardId,
                            entity.CardId,
                            StringComparison.Ordinal
                        );

                    bool transformedFromInitialCard =
                        !string.IsNullOrEmpty(
                            entity.Info.OriginalCardId
                        )
                        && string.Equals(
                            unit.InitialCardId,
                            entity.Info.OriginalCardId,
                            StringComparison.Ordinal
                        );

                    if (sameCard || transformedFromInitialCard)
                        return unit;
                }

                return null;
            }

            public CombatSideTotals CalculateTotals()
            {
                long totalAttack = 0;
                long totalHealth = 0;

                foreach (LogicalCombatUnit unit in _units)
                {
                    totalAttack +=
                        unit.PeakStats.MaximumAttack;

                    totalHealth +=
                        unit.PeakStats.MaximumHealth;
                }

                return new CombatSideTotals(
                    _units.Count,
                    totalAttack,
                    totalHealth
                );
            }

            public void Clear()
            {
                _units.Clear();
                _unitsByEntityId.Clear();
                _initialUnitsWaitingForCombatAlias.Clear();
            }
        }

        private sealed class CombatSideTracker
        {
            private readonly Dictionary<int, UnitPeakStats>
                _units =
                    new Dictionary<int, UnitPeakStats>();

            public void Observe(
                IEnumerable<Entity> entities)
            {
                if (entities == null)
                    return;

                foreach (Entity entity in entities)
                {
                    if (entity == null || entity.Id <= 0)
                        continue;

                    UnitPeakStats unit;

                    if (
                        !_units.TryGetValue(
                            entity.Id,
                            out unit
                        )
                    )
                    {
                        unit = new UnitPeakStats();
                        _units.Add(entity.Id, unit);
                    }

                    unit.Observe(entity);
                }
            }

            public CombatSideTotals CalculateTotals()
            {
                long totalAttack = 0;
                long totalHealth = 0;

                foreach (
                    UnitPeakStats unit
                    in _units.Values
                )
                {
                    totalAttack += unit.MaximumAttack;
                    totalHealth += unit.MaximumHealth;
                }

                return new CombatSideTotals(
                    _units.Count,
                    totalAttack,
                    totalHealth
                );
            }

            public void Clear()
            {
                _units.Clear();
            }
        }

        private sealed class LogicalCombatUnit
        {
            public int InitialEntityId { get; private set; }
            public string InitialCardId { get; private set; }
            public int InitialZonePosition { get; private set; }
            public int CombatAliasEntityId { get; set; }
            public UnitPeakStats PeakStats { get; private set; }

            private LogicalCombatUnit()
            {
                InitialCardId = string.Empty;
                PeakStats = new UnitPeakStats();
            }

            public static LogicalCombatUnit FromInitialSnapshot(
                InitialUnitSnapshot snapshot)
            {
                LogicalCombatUnit unit =
                    new LogicalCombatUnit
                    {
                        InitialEntityId = snapshot.EntityId,
                        InitialCardId = snapshot.CardId,
                        InitialZonePosition =
                            snapshot.ZonePosition
                    };

                unit.PeakStats.ObserveValues(
                    snapshot.Attack,
                    snapshot.MaximumHealth
                );

                return unit;
            }

            public static LogicalCombatUnit FromSummonedEntity(
                Entity entity)
            {
                LogicalCombatUnit unit =
                    new LogicalCombatUnit
                    {
                        CombatAliasEntityId = entity.Id
                    };

                unit.PeakStats.Observe(entity);
                return unit;
            }
        }

        private sealed class InitialUnitSnapshot
        {
            public int EntityId { get; private set; }
            public string CardId { get; private set; }
            public int ZonePosition { get; private set; }
            public long Attack { get; private set; }
            public long MaximumHealth { get; private set; }

            private InitialUnitSnapshot()
            {
                CardId = string.Empty;
            }

            public static InitialUnitSnapshot FromEntity(
                Entity entity)
            {
                return new InitialUnitSnapshot
                {
                    EntityId = entity.Id,
                    CardId = entity.CardId ?? string.Empty,
                    ZonePosition = entity.ZonePosition,
                    Attack = Math.Max(0, entity.Attack),
                    MaximumHealth = Math.Max(
                        0,
                        entity.GetTag(GameTag.HEALTH)
                    )
                };
            }
        }

        private sealed class UnitPeakStats
        {
            public long MaximumAttack { get; private set; }
            public long MaximumHealth { get; private set; }

            public void Observe(Entity entity)
            {
                ObserveValues(
                    Math.Max(0, entity.Attack),
                    Math.Max(
                        0,
                        entity.GetTag(GameTag.HEALTH)
                    )
                );
            }

            public void ObserveValues(
                long attack,
                long maximumHealth)
            {
                if (attack > MaximumAttack)
                    MaximumAttack = attack;

                if (maximumHealth > MaximumHealth)
                    MaximumHealth = maximumHealth;
            }
        }

        private struct BoardTotals
        {
            public long Attack { get; }
            public long Health { get; }

            public BoardTotals(
                long attack,
                long health)
            {
                Attack = attack;
                Health = health;
            }
        }

        private struct CombatSideTotals
        {
            public long Units { get; }
            public long Attack { get; }
            public long Health { get; }

            public CombatSideTotals(
                long units,
                long attack,
                long health)
            {
                Units = units;
                Attack = attack;
                Health = health;
            }
        }

        private struct CombatSummary
        {
            public static CombatSummary Empty =>
                new CombatSummary(
                    new CombatSideTotals(0, 0, 0),
                    new CombatSideTotals(0, 0, 0)
                );

            public CombatSideTotals Player { get; }
            public CombatSideTotals Opponent { get; }

            public CombatSummary(
                CombatSideTotals player,
                CombatSideTotals opponent)
            {
                Player = player;
                Opponent = opponent;
            }
        }
    }
}
