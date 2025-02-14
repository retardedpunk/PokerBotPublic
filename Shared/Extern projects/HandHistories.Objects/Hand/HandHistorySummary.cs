﻿using System;
using System.Collections.Generic;
using HandHistories.Objects.Actions;
using HandHistories.Objects.Cards;
using HandHistories.Objects.GameDescription;
using HandHistories.Objects.Players;

namespace HandHistories.Objects.Hand
{
    public class HandHistorySummary
    {
        public HandHistorySummary()
        {           
            GameDescription = new GameDescriptor();            
        }

        public DateTime DateOfHandUtc { get; set; }

        public long HandId { get; set; }

        public int DealerButtonPosition { get; set; }

        public string TableName { get; set; }

        public GameDescriptor GameDescription { get; set; }

        public bool Cancelled { get; set; }

        public int NumPlayersSeated { get; set; }

        public string FullHandHistoryText { get; set; }

        /// <summary>
        /// The sum of all bets and blinds excluding uncalled bets
        /// </summary>
        public double? TotalPot { get; set; }

        public double? Rake { get; set; }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            HandHistorySummary hand = obj as HandHistorySummary;
            if (hand == null) return false;

            return ToString().Equals(hand.ToString());
        }
           
        public override string ToString()
        {
            return string.Format("[{0}] {1}", HandId, GameDescription.ToString());
        }
    }
}
