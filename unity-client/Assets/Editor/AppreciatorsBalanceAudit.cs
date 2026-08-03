using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AppreciatorsTcg.AI;
using AppreciatorsTcg.Battle;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using UnityEditor;
using UnityEngine;

namespace AppreciatorsTcg.EditorTools
{
    public static class AppreciatorsBalanceAudit
    {
        private const int SimulationCount = 5000;
        private const int Seed = 20260621;

        [MenuItem("Appreciators/Run 5,000 Match Balance Audit")]
        public static void RunFromMenu()
        {
            Run();
        }

        public static void Run()
        {
            IReadOnlyList<CardDefinition> catalog = CardCatalog.AllCards;
            if (catalog.Count < GameConstants.DeckSize)
            {
                throw new InvalidOperationException("Balance audit requires at least 12 cards.");
            }

            Dictionary<string, CardStat> stats = catalog.ToDictionary(
                card => card.id,
                card => new CardStat(card));
            System.Random random = new System.Random(Seed);
            int playerWins = 0;
            int opponentWins = 0;
            int draws = 0;

            for (int matchIndex = 0; matchIndex < SimulationCount; matchIndex++)
            {
                List<CardDefinition> playerDeck = BuildRandomDeck(catalog, random);
                List<CardDefinition> opponentDeck = BuildRandomDeck(catalog, random);
                BattleGame game = new BattleGame("Balance A", playerDeck, opponentDeck, Seed + matchIndex * 37);
                game.Start();

                while (!game.IsComplete)
                {
                    SimpleAiPlayer.PlayTurn(game, OwnerSide.Player, random);
                    SimpleAiPlayer.PlayTurn(game, OwnerSide.Opponent, random);
                    game.TryInvestCommunityShield(OwnerSide.Player, out _);
                    game.TryInvestCommunityRally(OwnerSide.Player, out _);
                    game.TryInvestCommunityShield(OwnerSide.Opponent, out _);
                    game.TryInvestCommunityRally(OwnerSide.Opponent, out _);
                    game.EndPlayerTurnOnly();
                }

                string winner = MatchResultData.LastResult?.winner ?? "Draw";
                bool playerWon = winner == "Victory";
                bool opponentWon = winner == "Defeat";
                if (playerWon)
                {
                    playerWins += 1;
                }
                else if (opponentWon)
                {
                    opponentWins += 1;
                }
                else
                {
                    draws += 1;
                }

                RecordDeck(stats, playerDeck, playerWon, !playerWon && !opponentWon);
                RecordDeck(stats, opponentDeck, opponentWon, !playerWon && !opponentWon);
            }

            string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "BALANCE_REPORT.md"));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? string.Empty);
            File.WriteAllText(reportPath, BuildReport(stats.Values, playerWins, opponentWins, draws), Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"Balance audit complete: {SimulationCount:N0} matches. Report: {reportPath}");
        }

        private static List<CardDefinition> BuildRandomDeck(IReadOnlyList<CardDefinition> catalog, System.Random random)
        {
            return catalog
                .OrderBy(_ => random.Next())
                .Take(GameConstants.DeckSize)
                .ToList();
        }

        private static void RecordDeck(Dictionary<string, CardStat> stats, IEnumerable<CardDefinition> deck, bool won, bool draw)
        {
            foreach (CardDefinition card in deck)
            {
                CardStat stat = stats[card.id];
                stat.Inclusions += 1;
                if (won)
                {
                    stat.Wins += 1;
                }
                else if (draw)
                {
                    stat.Draws += 1;
                }
            }
        }

        private static string BuildReport(IEnumerable<CardStat> values, int playerWins, int opponentWins, int draws)
        {
            List<CardStat> sorted = values
                .OrderByDescending(value => value.ScoredWinRate)
                .ThenBy(value => value.Card.cost)
                .ToList();
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Appreciators TCG Balance Audit");
            builder.AppendLine();
            builder.AppendLine($"- Simulations: {SimulationCount:N0}");
            builder.AppendLine($"- Seed: {Seed}");
            builder.AppendLine($"- First-side wins: {playerWins:N0}");
            builder.AppendLine($"- Second-side wins: {opponentWins:N0}");
            builder.AppendLine($"- Draws: {draws:N0}");
            builder.AppendLine("- Method: two identical production AIs, random unique 12-card decks, six turns, simultaneous paired combat, and automatic Community ward/rally investment");
            builder.AppendLine();
            builder.AppendLine("A draw counts as half a win. Inclusion win rate is directional evidence, not a substitute for human playtesting.");
            builder.AppendLine();
            builder.AppendLine("| Card | Cost | Power | App | Included | Scored win rate | Delta |");
            builder.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
            foreach (CardStat stat in sorted)
            {
                double delta = stat.ScoredWinRate - 0.5d;
                builder.AppendLine(
                    $"| {stat.Card.name} | {stat.Card.cost} | {stat.Card.power} | {stat.Card.appreciation} | {stat.Inclusions:N0} | {stat.ScoredWinRate.ToString("P1", CultureInfo.InvariantCulture)} | {delta.ToString("+0.0%;-0.0%;0.0%", CultureInfo.InvariantCulture)} |");
            }

            builder.AppendLine();
            builder.AppendLine("## Review Thresholds");
            builder.AppendLine();
            builder.AppendLine("Cards beyond +/-3 percentage points are flagged for a manual rules review. Final tuning still requires human matches because this AI cannot model bluffing, sequencing intent, or matchup knowledge.");
            return builder.ToString();
        }

        private sealed class CardStat
        {
            public CardStat(CardDefinition card)
            {
                Card = card;
            }

            public CardDefinition Card { get; }
            public int Inclusions { get; set; }
            public int Wins { get; set; }
            public int Draws { get; set; }
            public double ScoredWinRate => Inclusions == 0 ? 0d : (Wins + Draws * 0.5d) / Inclusions;
        }
    }
}
