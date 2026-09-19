using System.Collections.Generic;

namespace Dovaky.Combat.Net
{
    /// <summary>État complet d'un combattant, tel qu'un client le reçoit en arrivant.</summary>
    public sealed class FighterSnapshot
    {
        public int Id;
        public string Name;
        public int TeamId;
        public Cell Position;
        public int Health;
        public int MaxHealth;
        public int ActionPoints;
        public int MaxActionPoints;
        public int MovementPoints;
        public int MaxMovementPoints;
    }

    /// <summary>
    /// Photographie du combat envoyée à un client qui se connecte ou se
    /// reconnecte : à partir de là, les événements suffisent à rester à jour.
    /// </summary>
    public sealed class BattleSnapshot
    {
        public int Width;
        public int Height;

        /// <summary>Nature de chaque case, ligne par ligne (voir <see cref="CellKind"/>).</summary>
        public byte[] Cells;

        public List<FighterSnapshot> Fighters = new List<FighterSnapshot>();

        public int Round;
        public int ActiveFighterId;
        public bool IsOver;
        public int? WinningTeamId;

        public static BattleSnapshot Capture(Battle battle)
        {
            var snapshot = new BattleSnapshot
            {
                Width = battle.Map.Width,
                Height = battle.Map.Height,
                Cells = new byte[battle.Map.Width * battle.Map.Height],
                Round = battle.Round,
                ActiveFighterId = battle.ActiveFighter != null ? battle.ActiveFighter.Id : 0,
                IsOver = battle.IsOver,
                WinningTeamId = battle.WinningTeamId,
            };

            for (int y = 0; y < battle.Map.Height; y++)
            {
                for (int x = 0; x < battle.Map.Width; x++)
                {
                    snapshot.Cells[y * battle.Map.Width + x] = (byte)battle.Map.GetCell(new Cell(x, y));
                }
            }

            foreach (Fighter fighter in battle.Fighters)
            {
                snapshot.Fighters.Add(new FighterSnapshot
                {
                    Id = fighter.Id,
                    Name = fighter.Name,
                    TeamId = fighter.TeamId,
                    Position = fighter.Position,
                    Health = fighter.Health,
                    MaxHealth = fighter.MaxHealth,
                    ActionPoints = fighter.ActionPoints,
                    MaxActionPoints = fighter.MaxActionPoints,
                    MovementPoints = fighter.MovementPoints,
                    MaxMovementPoints = fighter.MaxMovementPoints,
                });
            }

            return snapshot;
        }
    }
}
