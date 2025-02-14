﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HandHistories.Objects.Actions;
using HandHistories.Objects.Cards;
using HandHistories.Objects.GameDescription;
using HandHistories.Objects.Players;
using HandHistories.Parser.Parsers.Exceptions;
using HandHistories.Parser.Parsers.FastParser.Base;
using HandHistories.Parser.Utils.Strings;
using System.Globalization;
using HandHistories.Objects.Hand;
using HandHistories.Parser.Parsers.Base;
using HandHistories.Parser.Utils.FastParsing;

namespace HandHistories.Parser.Parsers.FastParser.FullTiltPoker
{
    public sealed class FullTiltPokerFastParserImpl : HandHistoryParserFastImpl, IThreeStateParser
    {
        public override SiteName SiteName
        {
            get { return SiteName.FullTilt; }
        }

        // we adjust the raise sizes on our own during FixUncalledBets
        public override bool RequiresAdjustedRaiseSizes
        {
            get { return false; }
        }

        public override bool SupportRunItTwice
        {
            get
            {
                return true;
            }
        }

        private static readonly NumberFormatInfo NumberFormatInfo = new NumberFormatInfo
            {
                NegativeSign = "-",
                CurrencyDecimalSeparator = ".",
                CurrencyGroupSeparator = ",",
                CurrencySymbol = "$"
            };

        private static readonly Regex HandSplitRegex = new Regex("(Full Tilt Poker Game #)", RegexOptions.Compiled);

        public override IEnumerable<string> SplitUpMultipleHands(string rawHandHistories)
        {
            return HandSplitRegex.Split(rawHandHistories)
                                 .Where(s => string.IsNullOrWhiteSpace(s) == false && s.Length > 30)
                                 .Select(s => "Full Tilt Poker Game #" + s.Trim('\r', 'n')); 
        }

        protected override int ParseDealerPosition(string[] handLines)
        {
            // The button is in seat #5

            for (int i = 2; i < handLines.Length - 1; i++)
            {
                string handLine = handLines[i];

                if (handLine.StartsWith("The button "))
                {
                    int button = int.Parse(handLine[handLine.Length - 1].ToString());

                    if (button == 0)
                    {
                        return 10;
                    }

                    return button;
                }
            }

            throw new InvalidHandException("Couldn't find dealer button line.");
        }

        protected override DateTime ParseDateUtc(string[] handLines)
        {
            // Full Tilt Poker Game #26862429938: Table Adornment (6 max, shallow) - $0.50/$1 - No Limit Hold'em - 16:07:17 ET - 2010/12/31

            string [] split = handLines[0].Split('-');
            string timeString = split[3]; // ' 16:07:17 ET '
            string dateString = split[4];//  ' 2010/12/31 '

            int year = FastInt.Parse(dateString.Substring(1, 4));
            int month = FastInt.Parse(dateString.Substring(6, 2));
            int day = FastInt.Parse(dateString.Substring(9, 2));

            int hour = FastInt.Parse(timeString.Substring(1, 2));
            int minute = FastInt.Parse(timeString.Substring(4, 2));
            int second = FastInt.Parse(timeString.Substring(7, 2));

            DateTime dateTime = new DateTime(year, month, day, hour, minute, second);
            DateTime converted = TimeZoneInfo.ConvertTimeToUtc(dateTime, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

            return converted;
        }

        protected override PokerFormat ParsePokerFormat(string[] handLines)
        {
            return PokerFormat.CashGame;
        }

        protected override long ParseHandId(string[] handLines)
        {
            // Full Tilt Poker Game #26862468195: Table Adornment (6 max, shallow) - $0.50/$1 - No Limit Hold'em - 16:09:19 ET - 2010/12/31
            string line = handLines[0];

            int hashIndex = 21;
            int colonIndex = line.IndexOf(':', hashIndex);

            string handNumber = line.Substring(hashIndex + 1, colonIndex - hashIndex - 1);
            return long.Parse(handNumber);
        }

        protected override long ParseTournamentId(string[] handLines)
        {
            throw new NotImplementedException();
        }

        protected override string ParseTableName(string[] handLines)
        {
            // Full Tilt Poker Game #28617512574: Table Bri (6 max) - $0.25/$0.50 - $15 Cap No Limit Hold'em - 18:46:08 ET - 2011/02/28

            string table = handLines[0].Split('-')[0].Split(':')[1].Split('(')[0];
            table = table.Replace("Table", "");
            table = table.Trim();

            return table;
        }

        protected override SeatType ParseSeatType(string[] handLines)
        {
            // Full Tilt Poker Game #26862468195: Table Adornment (6 max, shallow) - $0.50/$1 - No Limit Hold'em - 16:09:19 ET - 2010/12/31

            string handLine = handLines[0];
            string [] seatInfo = handLine.Split('(');

            // No ( means its full ring
            if (seatInfo.Length == 1)
            {
                return SeatType.FromMaxPlayers(9);
            }

            if (seatInfo[1].StartsWith("6 max"))
            {
                return SeatType.FromMaxPlayers(6);
            }
            else if (seatInfo[1].StartsWith("heads"))
            {
                return SeatType.FromMaxPlayers(2);
            }
            
            return SeatType.FromMaxPlayers(9);
        }

        protected override GameType ParseGameType(string[] handLines)
        {
            // OLD: Full Tilt Poker Game #26862468195: Table Adornment (6 max, shallow) - $0.50/$1 - No Limit Hold'em - 16:09:19 ET - 2010/12/31
            // NEW: Full Tilt Poker Game #26862468195: Table Adornment (6 max, shallow) - NL Hold'em - $0.50/$1 - 16:09:19 ET - 2010/12/31

            // we add this code in order to make it work for both starting lines
            string gameTypeString;
            string[] splitter = handLines[0].Split('-');
            if (splitter[2].Contains("/"))
            {
                gameTypeString = splitter[1];
            }
            else
            {
                gameTypeString = splitter[2];
            }

            switch (gameTypeString)
            {
                case " FL Omaha H/L ":
                case " Limit Omaha H/L ":
                    return GameType.FixedLimitOmahaHiLo;
                case " FL Omaha":
                case " Limit Omaha":
                    return GameType.FixedLimitOmaha;
                case " CAP NL Hold'em ":
                case " Cap No Limit Hold'em":
                case " NL Hold'em ":
                case " No Limit Hold'em ":
                    return GameType.NoLimitHoldem;
                case " CAP PL Omaha Hi ":
                case " Cap Pot Limit Omaha Hi ":
                case " PL Omaha Hi ":
                case " Pot Limit Omaha Hi ":
                    return GameType.PotLimitOmaha;
                case " Cap Pot Limit Omaha H/L ":
                case " CAP PL Omaha H/L ":
                case " PL Omaha H/L ":
                case " Pot Limit Omaha H/L ":
                    return GameType.PotLimitOmahaHiLo;
                case " FL Hold'em ":
                case " Limit Hold'em ":
                    return GameType.FixedLimitHoldem;
                case " NL Omaha H/L ":
                case " No Limit Omaha H/L ":
                    return GameType.NoLimitOmahaHiLo;               
                default:
                    throw new UnrecognizedGameTypeException(handLines[0], "Did not recognize.");
            }
        }

        protected override TableType ParseTableType(string[] handLines)
        {
            bool isCap = handLines[0].ToLower().Contains(" cap ");

            return TableType.FromTableTypeDescriptions(isCap ? TableTypeDescription.Cap : TableTypeDescription.Regular);
        }

        protected override Limit ParseLimit(string[] handLines)
        {
            // Expect the first line to look like:
            // OLD: Full Tilt Poker Game #28617512574: Table Bri (6 max) - $0.25/$0.50 - $15 Cap No Limit Hold'em - 18:46:08 ET - 2011/02/28
            // NEW: Full Tilt Poker Game #28617512574: Table Bri (6 max) - $15 Cap No Limit Hold'em - $0.25/$0.50- 18:46:08 ET - 2011/02/28
            
            // we add this code in order to make it work for both starting lines
            string limit;
            string[] splitter = handLines[0].Split('-');
            if(splitter[2].Contains("/"))
            {
                limit = splitter[2];
            }
            else
            {
                limit = splitter[1];
            }
            string limitSubstring = limit.Trim();

            char currencySymbol = limitSubstring[0];
            Currency currency;

            switch (currencySymbol)
            {
                case '$':
                    currency = Currency.USD;
                    NumberFormatInfo.CurrencySymbol = "$";
                    break;
                case '€':
                    currency = Currency.EURO;
                    NumberFormatInfo.CurrencySymbol = "€";
                    break;
                case '£':
                    currency = Currency.GBP;
                    NumberFormatInfo.CurrencySymbol = "£";
                    break;
                default:
                    throw new LimitException(handLines[0], "Unrecognized currency symbol " + currencySymbol);
            }

            // we don't care for the ante amount, so just throw it away
            if (limitSubstring.Contains("Ante"))
            {
                limitSubstring = limitSubstring.Split(' ')[0];
            }

            int slashIndex = limitSubstring.IndexOf('/');

            double small = double.Parse(limitSubstring.Substring(0, slashIndex), NumberStyles.AllowCurrencySymbol | NumberStyles.Number, NumberFormatInfo);
            string bigString = limitSubstring.Substring(slashIndex + 1, limitSubstring.Length - slashIndex - 1);
            double big = double.Parse(bigString, NumberStyles.AllowCurrencySymbol | NumberStyles.Number, NumberFormatInfo);

            double ante = 0;
            bool isAnte = false;

            return Limit.FromSmallBlindBigBlind(small, big, currency, isAnte, ante);
        }

        protected override Buyin ParseBuyin(string[] handLines)
        {
            throw new NotImplementedException();
        }

        public override bool IsValidHand(string[] handLines)
        {
            return (handLines[1].StartsWith("Seat "));
        }

        public override bool IsValidOrCancelledHand(string[] handLines, out bool isCancelled)
        {
            isCancelled = false;
            for (int i = handLines.Length - 1; i > 2; i--)
            {
                var line = handLines[i];

                // we search for 
                // Hand #35550557071 has been canceled
                if (line[0] == 'H' && line[5] == '#' && line[line.Length - 1] == 'd')
                {
                    isCancelled = true;
                    break;
                }
            }
            return IsValidHand(handLines);
        }

        protected override List<HandAction> ParseHandActions(string[] handLines, GameType gameType = GameType.Unknown)
        {
            // this is needed for future uncalledbet fixes
            var handHistory = new HandHistory();
            ParseExtraHandInformation(handLines, handHistory);

            var actions = new List<HandAction>(handLines.Length);
            // TODO: implement
            int startIndex = FindHandActionsStart(handLines);

            startIndex = ParseBlindActions(handLines, ref actions, startIndex);

            Street currentStreet = Street.Preflop;

            for (int i = startIndex; i < handLines.Length; i++)
            {
                var line = handLines[i];

                if (IsChatLine(line))
                {
                    continue;
                }

                if (IsUncalledBetLine(line))
                {
                    actions.Add(ParseUncalledBet(line, currentStreet));
                    ParseShowDown(handLines, ref actions, i + 1, gameType);
                    return FixUncalledBets(actions, handHistory.TotalPot, handHistory.Rake);
                }

                if (line.Contains(" shows ["))
                {
                    ParseShowDown(handLines, ref actions, i, GameType.Unknown);

                    return FixUncalledBets(actions, handHistory.TotalPot, handHistory.Rake);
                }

                var lastChar = line[line.Length - 1];

                HandAction action;

                switch (lastChar)
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        action = ParseActionWithAmount(line, currentStreet);
                        if (action != null)
                        {
                            actions.Add(action);
                        }
                        continue;

                    //theking881 calls $138, and is all in
                    //draggstar sits down
                    case 'n':
                        if (line.FastEndsWith("and is all in"))
                        {
                            action = ParseActionWithAmount(line.Remove(line.Length - 15), currentStreet, true);//", and is all in".Length
                            if (action != null)
                            {
                                actions.Add(action);
                            }
                        }
                        continue;

                    case ')':
                        action = ParseWinActionOrStreet(line, ref currentStreet);
                        if (action != null)
                        {
                            actions.Add(action);
                        }
                        break;

                    //jobetzu checks
                    //jobetzu folds
                    //theking881 mucks
                    case 's':
                        action = ParseFoldCheckLine(line, currentStreet);
                        if (action != null)
                        {
                            actions.Add(action);
                        }
                        continue;

                    // 02nina20 calls $0.04, and is capped
                    case 'd':
                        if (line[line.Length - 3] == 'p')
                        {
                            action = ParseActionWithAmount(line.Remove(line.LastIndexOf(',')), currentStreet);
                            if (action != null)
                            {
                                actions.Add(action);
                            }
                        }
                        continue;

                    //*** SHOW DOWN ***
                    //*** SUMMARY ***
                    case '*':
                        ParseShowDown(handLines, ref actions, i, GameType.Unknown);

                        return FixUncalledBets(actions, handHistory.TotalPot, handHistory.Rake);

                    //Dealt to FT_Hero [Qh 5c]
                    //Postrail shows [Qs Ah]
                    case ']':
                        if (line.IndexOf(" shows [", StringComparison.Ordinal) != -1)
                        {
                            ParseShowDown(handLines, ref actions, i, GameType.Unknown);
                            return FixUncalledBets(actions, handHistory.TotalPot, handHistory.Rake);
                        }
                        continue;

                    //Opponent3 has requested TIME
                    //jobetzu has 15 seconds left to act
                    default:
                        continue;
                }
            }

            return FixUncalledBets(actions, handHistory.TotalPot, handHistory.Rake);
        }

        static bool IsChatLine(string line)
        {
            //Example chat line
            //Player2: 1o21

            if (line.IndexOf(": ", StringComparison.Ordinal) == -1)
            {
                return false;
            }

            //*** FLOP *** [As Kc 2d] (Total Pot: $58.50, 2 Players)
            if (line[0] == '*' && line[line.Length - 1] == ')')
            {
                return false;
            }
            return true;
        }


        public void ParseShowDown(string[] handLines, ref List<HandAction> actions, int lineIndex, GameType gametype)
        {
            for (int i = lineIndex; i < handLines.Length; i++)
            {
                var line = handLines[i];

                if (line.EndsWith(" mucks"))
                {
                    actions.Add(new HandAction(line.Remove(line.Length - 6), HandActionType.MUCKS, 0.0, Street.Showdown));
                }

                else if (line.Contains(" wins "))
                {
                    int nameEndIndex = -1;
                    nameEndIndex = line.IndexOf(" wins the pot (", StringComparison.Ordinal);
                    if (nameEndIndex == -1)
                    {
                        nameEndIndex = line.IndexOf(" wins pot 1 (", StringComparison.Ordinal);
                    }

                    if (nameEndIndex == -1)
                    {
                        continue;
                    }

                    string playerName = ObtainPlayerNameFromShowdownLine(handLines, i);
                    if (string.IsNullOrWhiteSpace(playerName))
                    {
                        playerName = line.Remove(nameEndIndex);
                    }

                    int amountStartIndex = line.IndexOf('(', nameEndIndex) + 1;
                    int amountEndString = line.IndexOf(')', amountStartIndex);

                    string amountString = line.Substring(amountStartIndex, amountEndString - amountStartIndex);
                    double amount = double.Parse(amountString, NumberStyles.AllowCurrencySymbol | NumberStyles.Number, NumberFormatInfo);

                    actions.Add(new WinningsAction(playerName, HandActionType.WINS, amount, 0));
                }
                else if (line.Contains(" shows ["))
                {
                    int nameEndIndex = line.IndexOf(" shows [", StringComparison.Ordinal);
                    string playerName = line.Remove(nameEndIndex);

                    actions.Add(new HandAction(playerName, HandActionType.SHOW, 0.0, Street.Showdown));
                }

#warning duplicate parse winning actions
                //We already parse winning actions, or is this a better way to parse winning actions even though it's slower?
                //
                //else if (line.Contains(" and won "))
                //{
                //    // as it's not absolutely clear what the name is, we grab the name out of the seating list
                //    //    Seat 6: Psipsis showed [Ad 5d] and won ($0.51) with a pair of Aces
                //    //    Seat 6: Psipsis (big blind) showed [Ad 5d] and won ($0.51) with a pair of Aces
                //    // -> Seat 6: Psipsis ($0.85)
                //    string playerName = ObtainPlayerNameFromShowdownLine(handLines, i);
                //    if (string.IsNullOrWhiteSpace(playerName)) continue;

                //    int amountStartIndex = line.IndexOf('(', line.IndexOf(" and won ", StringComparison.Ordinal)) + 1;
                //    int amountEndString = line.IndexOf(')', amountStartIndex);

                //    string amountString = line.Substring(amountStartIndex, amountEndString - amountStartIndex);
                //    double amount = double.Parse(amountString, NumberStyles.AllowCurrencySymbol | NumberStyles.Number, NumberFormatInfo);

                //    actions.Add(new WinningsAction(playerName, HandActionType.WINS, amount, 0));

                //}
                else if (line.Contains(" collected ("))
                {
                    if (actions.Any(a => a.IsWinningsAction)) continue;
                    // as it's not absolutely clear what the name is, we grab the name out of the seating list
                    // Seat 2: dude6974 (small blind) collected ($0.10), mucked
                    // Seat 7: tatskie100 collected ($3.35)
                    string playerName = ObtainPlayerNameFromShowdownLine(handLines, i);
                    if (string.IsNullOrWhiteSpace(playerName)) continue;

                    int amountStartIndex = line.LastIndexOf('(') + 1;
                    int amountEndString = line.IndexOf(')', amountStartIndex);

                    string amountString = line.Substring(amountStartIndex, amountEndString - amountStartIndex);
                    double amount = double.Parse(amountString, NumberStyles.AllowCurrencySymbol | NumberStyles.Number, NumberFormatInfo);

                    actions.Add(new WinningsAction(playerName, HandActionType.WINS, amount, 0));
                }
            }
        }

        private string ObtainPlayerNameFromShowdownLine(string[] handLines, int index)
        {
            var line = handLines[index];
            int seatIndex = line.LastIndexOf("Seat ", StringComparison.Ordinal);
            if (seatIndex < 0) return null;

            string seat = line.Substring(line.LastIndexOf("Seat ", StringComparison.Ordinal), 7);
            for (var k = 0; k <= 10; k++)
            {
                if (handLines[k].StartsWith(seat))
                {
                    int colonIndex = handLines[k].IndexOf(':', 5);
                    int parenIndex = handLines[k].IndexOf('(', colonIndex + 2);

                    return handLines[k].Substring(colonIndex + 2, parenIndex - 1 - colonIndex - 2);
                }
            }
            return null;
        }

        static HandAction ParseWinActionOrStreet(string line, ref Street currentStreet)
        {
            char idChar = line[line.Length - 2];

            switch (idChar)
            {
                //*** FLOP *** [Ad 5d 5c] (Total Pot: $165, 2 Players)
                case 's':
                //*** FLOP *** [Ad 5d 5c] (Total Pot: $165, 2 Players, 2 All-In)
                case 'n':
                    currentStreet = ParseStreet(line);
                    return null;

                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    return ParseWinAction(line);

                default:
                    throw new ArgumentException(string.Format("Unknown IdChar: '{0}' Line: {1}",
                        idChar,
                        line));
            }
        }

        static HandAction ParseWinAction(string line)
        {
            int amountStartIndex = line.IndexOf(NumberFormatInfo.CurrencySymbol, StringComparison.Ordinal);
            string amountString = line.Substring(amountStartIndex, line.Length - amountStartIndex - 1);

            double amount = double.Parse(amountString, NumberStyles.AllowCurrencySymbol | NumberStyles.Number, NumberFormatInfo);

            string playerName = line.Remove(amountStartIndex - 15); //" wins the pot (".Length

            return new WinningsAction(playerName, HandActionType.WINS, amount, 0);
        }

        static Street ParseStreet(string line)
        {
            char streetId = line[4];
            switch (streetId)
            {
                case 'F':
                    return Street.Flop;
                case 'T':
                    return Street.Turn;
                case 'R':
                    return Street.River;
            }
            throw new ArgumentException(string.Format("Unknown StreetID: {0} Line: {1}", streetId, line));
        }

        static HandAction ParseUncalledBet(string line, Street currentStreet)
        {
            const int amountStartIndex = 16;//"Uncalled bet of ".Length

            int amountEndIndex = line.IndexOf(' ', amountStartIndex);

            string amountString = line.Substring(amountStartIndex, amountEndIndex - amountStartIndex);

            double amount = double.Parse(amountString, NumberStyles.AllowCurrencySymbol | NumberStyles.Number, NumberFormatInfo);

            string playerName = line.Substring(amountEndIndex + 13);//" returned to ".Length

            return new HandAction(playerName, HandActionType.UNCALLED_BET, amount, currentStreet);
        }

        static bool IsUncalledBetLine(string line)
        {
            return line.StartsWith("Uncalled bet of ") && line.Contains(" returned to ");
        }



        static HandAction ParseActionWithAmount(string line, Street currentStreet, bool isAllIn = false)
        {
            int idIndex = line.LastIndexOf(' ');
            char idChar = line[idIndex - 3];

            string playerName;
            HandActionType actionType;
            double amount = ParseAmount(line, idIndex + 2);

            switch (idChar)
            {
                //Rene Lacoste bets $20
                case 'e':
                    playerName = line.Remove(idIndex - 5);
                    actionType = HandActionType.BET;
                    break;

                //ElkY calls $10
                case 'l':
                    playerName = line.Remove(idIndex - 6);
                    actionType = HandActionType.CALL;
                    break;

                //Rene Lacoste raises to $20
                case ' ':
                    playerName = line.Remove(idIndex - 10);
                    actionType = HandActionType.RAISE;
                    break;

                //jobetzu adds $30
                case 'd':
                    return null;

                default:
                    throw new ArgumentException(string.Format("Unhandled IdChar: {0} : Line: {1}",
                        idChar,
                        line));
            }

            return new HandAction(playerName, actionType, amount, currentStreet, isAllIn);
        }

        static double ParseAmount(string line, int startIndex)
        {
            var amountString = line.Substring(startIndex);
            return double.Parse(amountString, NumberStyles.AllowCurrencySymbol | NumberStyles.Number, NumberFormatInfo);
        }

        static HandAction ParseFoldCheckLine(string line, Street currentStreet)
        {
            char actionId = line[line.Length - 4];

            //ElkY folds
            if (actionId == 'o')
            {
                string playerName = line.Remove(line.Length - 6);
                return new HandAction(playerName, HandActionType.FOLD, 0.0, currentStreet);
            }
            //ElkY checks
            else if (actionId == 'e')
            {
                string playerName = line.Remove(line.Length - 7);
                return new HandAction(playerName, HandActionType.CHECK, 0.0, currentStreet);
            }
            //Rene Lacoste mucks
            else if (actionId == 'u')
            {
                string playerName = line.Remove(line.Length - 6);
                return new HandAction(playerName, HandActionType.MUCKS, 0.0, currentStreet);
            }

            // showdown lines can also occur here, without the *** SHOW DOWN *** line... this happens during allins
            // mukas72 wins the side pot ($0.26) with King High
            // Jurgu shows a pair of Tens, for high and 6,5,4,2,A, for low
            // mukas72 shows a pair of Kings
            // xxbugajusxx shows a pair of Threes
            // mukas72 wins side pot #1 ($0.26) with a pair of Kings
            // mukas72 wins the side pot ($0.26) with a pair of Kings
            // mukas72 wins the main pot ($0.26) with a pair of Kings
            // mukas72 wins the pot ($0.26) with a pair of Kings

            // we return null and don't add anything to the handactions
            return null;
        }

        public int ParseBlindActions(string[] handLines, ref List<HandAction> actions, int startIndex)
        {
            for (int i = startIndex; i < handLines.Length; i++)
            {
                bool isAllIn = false;
                string playerName;
                double amount;

                var line = handLines[i];

                if (IsChatLine(line))
                    continue;

                // Ante of $0.01 returned to balr1
                if (line[0] == 'A' && line[5] == 'o')
                {
                    var index = line.IndexOf(" returned to ", StringComparison.Ordinal);
                    if (index > -1)
                    {
                        playerName = line.Substring(index + 13);

                        line = line.Remove(index);

                        amount = ParseAmount(line, line.IndexOf(NumberFormatInfo.CurrencySymbol, StringComparison.Ordinal) + 1);

                        actions.Add(new HandAction(playerName, HandActionType.UNCALLED_BET, amount, Street.Preflop));
                        continue;
                    }
                }

                var lastChar = line[line.Length - 1];

                switch (lastChar)
                {
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        // ignore 'bulka adds $1'
                        if (line.Contains("adds $"))
                            continue;

                        break;

                    // dude6974 sits down
                    // milkman 046 posts the big blind of $0.05, and is all in                   
                    case 'n':
                        if (line[line.Length - 2] == 'i')
                        {
                            isAllIn = true;
                            line = line.Remove(line.LastIndexOf(','));
                            break;
                        }
                        continue;

                    //*** HOLE CARDS ***
                    case '*':
                        if (line[4] == 'H')
                        {
                            return i + 1;
                        }
                        return i;

                    default:
                        continue;
                }

                int idIndex = line.LastIndexOf(' ');
                char idChar = line[idIndex - 2];

                amount = ParseAmount(line, idIndex + 2);
                HandActionType actionType;

                switch (idChar)
                {
                    //Rene Lacoste posts the small blind of $5
                    //  Rene Lacoste posts the big blind of $5
                    //  IiIyMuK posts a dead small blind of $0.02
                    case 'o':
                        if (line[idIndex - 10] == 'l')
                        {
                            if (idIndex - 25 < 0)
                                throw new InvalidHandException(string.Join("\r\n", handLines));

                            // dead small blind
                            if (line[idIndex - 16] == 'd')
                            {
                                actionType = HandActionType.POSTS;
                                playerName = line.Remove(idIndex - 28);//" posts a dead small blind of".Length  
                            }
                            else
                            {
                                actionType = HandActionType.SMALL_BLIND;
                                playerName = line.Remove(idIndex - 25);//" posts the small blind of".Length 
                            }
                        }
                        else if (line[idIndex - 10] == 'g')
                        {
                            if (idIndex - 23 < 0)
                                throw new InvalidHandException(string.Join("\r\n", handLines));

                            actionType = HandActionType.BIG_BLIND;
                            playerName = line.Remove(idIndex - 23);//" posts the big blind of".Length  

                        }
                        else
                        {
                            throw new ArgumentException(string.Format("Unhandled idChar: '{0}' Line: {1}",
                             idChar,
                             line));
                        }
                        break;

                    //The button is in seat #3
                    case 'a':
                        continue;

                    //iason07 antes $0.30
                    case 'e':
                        actionType = HandActionType.ANTE;
                        playerName = line.Remove(idIndex - 6);
                        break;

                    // scrub52 posts $0.05
                    case 't':
                        actionType = HandActionType.POSTS;
                        playerName = line.Remove(idIndex - 6);//" posts".Length
                        break;

                    default:
                        throw new ArgumentException(string.Format("Unhandled idChar: '{0}' Line: {1}",
                            idChar,
                            line));
                }

                actions.Add(new HandAction(playerName, actionType, amount, Street.Preflop, isAllIn));
            }
            throw new ArgumentException("*** HOLE CARDS *** not found.");
        }

        static int FindHandActionsStart(string[] handLines)
        {
            for (int i = 1; i < handLines.Length; i++)
            {
                var line = handLines[i];
                switch (line[line.Length - 1])
                {
                    //Seat 1: Opponent1 ($400), is sitting out
                    case 't':
                    //Seat 1: jobetzu ($1,020)
                    case ')':
                        continue;
                    default:
                        return i;
                }
            }
            throw new ArgumentOutOfRangeException("Did not find HandActionStart");
        }

        protected override PlayerList ParsePlayers(string[] handLines)
        {
            PlayerList playerList = new PlayerList();

            for (int i = 1; i < 12; i++)
            {
                string handLine = handLines[i];
                var sittingOut = false;

                if (handLine.StartsWith("Seat ") == false)
                {
                    break;
                }

                if (handLine.EndsWith(")") == false)
                {
                    // handline is like Seat 6: ffbigfoot ($0.90), is sitting out
                    handLine = handLine.Substring(0, handLine.Length - 16);
                    sittingOut = true;
                }

                //Seat 1: CardBluff ($109.65)
                int colonIndex = handLine.IndexOf(':', 5);
                int parenIndex = handLine.IndexOf('(', colonIndex + 2);

                int seat = Int32.Parse(handLine.Substring(colonIndex - 2, 2));
                string name = handLine.Substring(colonIndex + 2, parenIndex - 1 - colonIndex - 2);
                string stackSizeString = handLine.Substring(parenIndex + 1, handLine.Length - 1 - parenIndex - 1);
                double amount = double.Parse(stackSizeString, NumberStyles.AllowCurrencySymbol | NumberStyles.Number, NumberFormatInfo);

                playerList.Add(new Player(name, amount, seat)
                    {
                        IsSittingOut = sittingOut
                    });
            }

            // OmahaHiLo has a different way of storing the hands at showdown, so we need to separate
            bool isOmahaHiLo = ParseGameType(handLines).Equals(GameType.PotLimitOmahaHiLo);

            int ShowStartIndex = -1;

            const int FirstPossibleShowActionIndex = 13; 
            for (int i = FirstPossibleShowActionIndex; i < handLines.Length; i++)
            {
                string line = handLines[i];
                if (line[line.Length - 1] == ']' && line.Contains(" shows ["))
                {
                    ShowStartIndex = i;
                    break;
                }
                if (line.StartsWith(@"*** SUM", StringComparison.Ordinal) && isOmahaHiLo)
                {
                    ShowStartIndex = i;
                    break;
                }
                else if (line.StartsWith(@"*** SH", StringComparison.Ordinal) && !isOmahaHiLo)
                {
                    ShowStartIndex = i;
                    break;
                }
            }

            if (ShowStartIndex != -1)
            {
                for (int lineNumber = ShowStartIndex; lineNumber < handLines.Length; lineNumber++)
                {
                    string line = handLines[lineNumber];

                    int firstSquareBracket = line.LastIndexOf('[');

                    if (firstSquareBracket == -1)
                    {
                        continue;
                    }

                    // can show single cards
                    if (line[firstSquareBracket + 3] == ']')
                    {
                        continue;
                    }

                    int lastSquareBracket = line.LastIndexLoopsBackward(']', line.Length - 1);
                    int colonIndex = line.LastIndexLoopsBackward(':', lastSquareBracket);

                    string playerName;
                    //if (isOmahaHiLo)
                    //{
                    //    seat = line.Substring(5, colonIndex - 5);
                    //    playerName = playerList.First(p => p.SeatNumber.Equals(Convert.ToInt32(seat))).PlayerName;
                    //}
                    //else
                    //{
                    int playerNameEndIndex = line.IndexOf(" shows", StringComparison.Ordinal);
                    if (playerNameEndIndex == -1)
                        break;

                    playerName = line.Substring(0, playerNameEndIndex);
                    //}

                    string cards = line.Substring(firstSquareBracket + 1, lastSquareBracket - (firstSquareBracket + 1));


                    playerList[playerName].HoleCards = HoleCards.FromCards(cards);
                }
            }

            return playerList;
        }

        protected override BoardCards ParseCommunityCards(string[] handLines)
        {
            // Expect the end of the hand to have something like this:
            /**** SUMMARY ***
            Total pot $7.75 | Rake $0.35
            Board: [9h Ts 8h 6c]
            Seat 1: hockenspit50 didn't bet (folded)
            Seat 2: drc40 folded on the Turn
            Seat 3: oggie the fox didn't bet (folded)
            Seat 4: diknek (button) collected ($7.40), mucked
            Seat 5: Naturalblow (small blind) folded before the Flop
            Seat 6: BeerMySole (big blind) folded before the Flop*/

            for (int lineNumber = handLines.Length - 2; lineNumber >= 0; lineNumber--)
            {
                string line = handLines[lineNumber];
                if (line[0] == '*')
                {
                    return BoardCards.ForPreflop();
                }

                if (line[0] != 'B')
                {
                    continue;
                }

                return ParseBoard(line);
            }

            throw new CardException(string.Empty, "Read through hand backwards and didn't find a board or summary.");
        }

        private static BoardCards ParseBoard(string line)
        {
            int firstSquareBracket = 7;
            int lastSquareBracket = line.Length - 1;

            string cards = line.Substring(firstSquareBracket + 1, lastSquareBracket - (firstSquareBracket + 1));
            return BoardCards.FromCards(cards);
        }

        protected override void ParseExtraHandInformation(string[] handLines, Objects.Hand.HandHistorySummary handHistorySummary)
        {
            for (int i = handLines.Length - 1; i >= 0; i--)
            {
                string line = handLines[i];
                if (line.StartsWith("*** SUMMARY ***", StringComparison.Ordinal))
                {
                    return;
                }

                // Total pot $42.90 | Rake $2.10            
                if (line[0] == 'T')
                {
                    int lastSpaceIndex = line.LastIndexOf(" ", StringComparison.Ordinal);
                    int spaceAfterFirstNumber = line.IndexOf(" ", 11, StringComparison.Ordinal);

                    handHistorySummary.Rake =
                        double.Parse(line.Substring(lastSpaceIndex + 1, line.Length - lastSpaceIndex - 1), NumberStyles.AllowCurrencySymbol | NumberStyles.Number, NumberFormatInfo);

                    handHistorySummary.TotalPot =
                        double.Parse(line.Substring(10, spaceAfterFirstNumber - 10), NumberStyles.AllowCurrencySymbol | NumberStyles.Number, NumberFormatInfo);

                    return;
                }
            }

            throw new Exception("Couldn't find sumamry line.");
        }

        const string DealtTo = "Dealt to ";
        static readonly int HeroNameStartIndex = DealtTo.Length;

        protected override string ParseHeroName(string[] handlines)
        {
            for (int i = 0; i < handlines.Length; i++)
            {
                string line = handlines[i];
                if (line[0] == 'D' && line.StartsWith(DealtTo))
                {
                    int HeroNameEndIndex = line.LastIndexOf('[') - 1;
                    return line.Substring(HeroNameStartIndex, HeroNameEndIndex - HeroNameStartIndex);
                }
            }
            return null;
        }

        public override RunItTwice ParseRunItTwice(string[] handLines)
        {
            bool isRunItTwiceHand = false;
            int RITScanIndex = -1;

            for (int i = handLines.Length - 1; i > 0; i--)
            {
                string line = handLines[i];

                if (line[0] != '*' )
                {
                    continue;
                }

                if (line == "*** SUMMARY 2 ***")
                {
                    RITScanIndex = i;
                    break;
                }
                else
                {
                    //this is not a run it twice hand
                    return null;
                }
            }

            RunItTwice RIT = new RunItTwice();
            //Parsing run it twice board
            for (int i = RITScanIndex; i < handLines.Length; i++)
            {
                string line = handLines[i];

                if (line[0] == 'B')
                {
                    RIT.Board = ParseBoard(line);
                    break;
                }
            }

            //Parsing run it twice showdown actions
            for (int i = RITScanIndex; i > 0; i--)
            {
                string line = handLines[i];

                if (line == "*** SHOW DOWN 2 ***")
                {
                    RITScanIndex = i;
                }
            }

            for (int i = RITScanIndex; i < handLines.Length; i++)
            {
                string line = handLines[i];

                if (line == "*** SUMMARY ***")
                {
                    break;
                }
                int nameEndIndex = line.IndexOf(" wins pot 2 (", StringComparison.Ordinal);
                if (nameEndIndex != -1)
                {
                    string playerName = line.Remove(nameEndIndex);

                    int amountStartIndex = line.IndexOf('(', nameEndIndex) + 2;
                    int amountEndString = line.IndexOf(')', amountStartIndex);

                    string amountString = line.Substring(amountStartIndex, amountEndString - amountStartIndex);
                    double amount = double.Parse(amountString, NumberFormatInfo);

                    RIT.Actions.Add(new WinningsAction(playerName, HandActionType.WINS, amount, 0));
                }
            }

            return RIT;
        }


        public int ParseGameActions(string[] handLines, ref List<HandAction> handActions, int firstActionIndex, out Street street)
        {
            throw new NotImplementedException();
        }

        private List<HandAction> FixUncalledBets(List<HandAction> handActions, double? totalPot, double? rake)
        {
            handActions = AdjustRaiseSizes(handActions);

            // this fix only takes place when the TotalPot - Rake != Winnings
            if (totalPot != null && rake != null)
            {
                var wagered = handActions.Where(a => !a.IsWinningsAction).Sum(a => a.Amount);
                if (totalPot + wagered == 0.0)
                {
                    return handActions;
                }

                var returnAmount = (double)(wagered + totalPot);
                var playerToReturnTo = handActions.Where(a => a.IsGameAction).GroupBy(a => a.PlayerName)
                                                        .Select(p => new
                                                        {
                                                            PlayerName = p.Key,
                                                            Invested = p.Sum(x => x.Amount)
                                                        }).OrderBy(x => x.Invested).First().PlayerName;

                handActions.Add(new HandAction(playerToReturnTo, HandActionType.UNCALLED_BET, returnAmount, Street.Showdown));

                return handActions;
            }

            var realActions = handActions.Where(a => a.IsGameAction && !a.IsWinningsAction && a.HandActionType != HandActionType.FOLD).ToList();

            var lastAction = realActions[realActions.Count - 1];

            // when the last action before summary is a bet, we need to return that bet to the according player
            if (lastAction.HandActionType == HandActionType.BET)
            {
                //if(!handActions.Any(a => a.PlayerName == lastAction.PlayerName && a.HandActionType == HandActionType.UNCALLED_BET && a.Amount == -lastAction.Amount))
                handActions.Add(new HandAction(lastAction.PlayerName, HandActionType.UNCALLED_BET, lastAction.Amount, Street.Showdown));
            }

            // when the last action before summary is a raise, we need to return the correct amount to the player
            if (lastAction.HandActionType == HandActionType.RAISE)
            {
                // amount to return is the amount the raise player invested - the 2nd largest amount invested by a different player
                var totalInvestedAmount = realActions.Where(a => a.PlayerName.Equals(lastAction.PlayerName)).Sum(a => a.Amount);

                // now we need to get the maximum amount invested by a different player involved in the hand
                var totalInvestedAmountOtherPlayer = realActions.Where(a => !a.PlayerName.Equals(lastAction.PlayerName)).GroupBy(a => a.PlayerName)
                                                                .Select(p => new
                                                                {
                                                                    PlayerName = p.Key,
                                                                    Invested = p.Sum(x => x.Amount)
                                                                })
                                                                .Min(x => x.Invested); // money invested is negative, so take the "max" negative value


                handActions.Add(new HandAction(lastAction.PlayerName, HandActionType.UNCALLED_BET, totalInvestedAmount - totalInvestedAmountOtherPlayer, Street.Showdown));
            }

            // when the last action before the summary is the big blind, we need to return the difference between BB and SB
            if (lastAction.HandActionType == HandActionType.BIG_BLIND)
            {
                // it can actually happen that there was no SB involved
                var sbAction = realActions.FirstOrDefault(a => a.HandActionType == HandActionType.SMALL_BLIND);
                var sbAmount = 0.0;
                if (sbAction != null) sbAmount = sbAction.Amount;

                handActions.Add(new HandAction(lastAction.PlayerName, HandActionType.UNCALLED_BET, lastAction.Amount - sbAmount, Street.Showdown));
            }

            return handActions;
        }
    }
}
