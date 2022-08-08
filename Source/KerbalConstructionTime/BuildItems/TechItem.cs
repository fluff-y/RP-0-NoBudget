﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KerbalConstructionTime
{
    [Flags]
    public enum NodeType
    {
        None = 0,

        Materials = 1 << 0,
        Electronics = 1 << 2,
        Bluesky = Materials | Electronics,

        Flight = 1 << 3,
        Spaceplanes = 1 << 4,
        Aerospace = Flight | Spaceplanes,

        Command = 1 << 5,
        Stations = 1 << 6,
        HSF = Command | Stations,

        Crewed = Aerospace | HSF,

        RCS = 1 << 7,

        EDL = 1 << 8,

        Hydrolox = 1 << 9,
        RocketEngines = 1 << 10,
        Staged = 1 << 11,
        LiquidEngines = Hydrolox | RocketEngines | Staged,

        Solid = 1 << 12,
        NTR = 1 << 14,
        Ion = 1 << 13,
        Propulsion = LiquidEngines | NTR | Ion,
        
        LifeSupport = 1 << 15,

        Nuclear = 1 << 16,
        Power = 1 << 17,
        Electricity = Nuclear | Power,

        Comms = 1 << 18,

        Avionics = 1 << 19,

        Science = 1 << 20,

        Any = ~0,
    }

    public class TechItem : IKCTBuildItem
    {
        private static readonly DateTime _epoch = new DateTime(1951, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public int ScienceCost;
        public int StartYear;
        public int EndYear;
        public string TechName, TechID;
        public double Progress;
        public ProtoTechNode ProtoNode;

        protected NodeType nodeType
        {
            get
            {
                if (KerbalConstructionTime.NodeTypes.TryGetValue(TechID, out var type))
                    return type;

                return NodeType.None;
            }
        }

        private double _buildRate = -1;
        private double _yearMult = -1;

        public double BuildRate
        {
            get
            {
                if (_buildRate < 0)
                {
                    UpdateBuildRate(Math.Max(KCTGameStates.TechList.IndexOf(this), 0));
                }
                return _buildRate;
            }
        }

        public double GetFractionComplete() => Progress / ScienceCost;

        public double TimeLeft => (ScienceCost - Progress) / BuildRate;

        public double YearBasedRateMult
        {
            get
            {
                if (_yearMult < 0)
                {
                    _yearMult = CalculateYearBasedRateMult();
                }
                return _yearMult;
            }
        }

        public TechItem(RDTech techNode)
        {
            ScienceCost = techNode.scienceCost;
            TechName = techNode.title;
            TechID = techNode.techID;
            Progress = 0;
            ProtoNode = ResearchAndDevelopment.Instance.GetTechState(TechID);

            if (KerbalConstructionTime.TechNodePeriods.TryGetValue(TechID, out KCTTechNodePeriod period))
            {
                StartYear = period.startYear;
                EndYear = period.endYear;
            }

            KCTDebug.Log("techID = " + TechID);
            KCTDebug.Log("TimeLeft = " + TimeLeft);
        }

        public TechItem() {}

        public TechItem(string ID, string name, double prog, int sci, int startYear, int endYear)
        {
            TechID = ID;
            TechName = name;
            Progress = prog;
            ScienceCost = sci;
            StartYear = startYear;
            EndYear = endYear;
        }

        public double UpdateBuildRate(int index)
        {
            ForceRecalculateYearBasedRateMult();
            double rate = Formula.GetResearchRate(ScienceCost, index, 0) * Utilities.GetResearcherEfficiencyMultipliers();
            if (rate < 0)
                rate = 0;

            if (rate != 0)
            {
                rate *= YearBasedRateMult;
                rate *= RP0.CurrencyUtils.Rate(RP0.TransactionReasonsRP0.RateResearch);
                rate *= RP0.Leaders.LeaderUtils.GetResearchRateEffect(nodeType, TechID);
            }

            _buildRate = rate;
            return _buildRate;
        }

        public void ForceRecalculateYearBasedRateMult()
        {
            _yearMult = -1;
        }

        public double CalculateYearBasedRateMult(double offset = 0)
        {
            if (StartYear < 1d || PresetManager.Instance.ActivePreset.GeneralSettings.YearBasedRateMult == null)
                return 1d;
            
            if (double.IsNaN(offset) || double.IsInfinity(offset) || offset * (1d / (86400d * 365.25d)) > 500d)
                return PresetManager.Instance.ActivePreset.GeneralSettings.YearBasedRateMult.Evaluate(PresetManager.Instance.ActivePreset.GeneralSettings.YearBasedRateMult.maxTime);

            DateTime curDate = _epoch.AddSeconds(Utilities.GetUT() + offset);

            double diffYears = (curDate - new DateTime(StartYear, 1, 1)).TotalDays / 365.25;
            if (diffYears > 0)
            {
                diffYears = (curDate - new DateTime(EndYear, 12, 31, 23, 59, 59)).TotalDays / 365.25;
                diffYears = Math.Max(0, diffYears);
            }
            return PresetManager.Instance.ActivePreset.GeneralSettings.YearBasedRateMult.Evaluate((float)diffYears);
        }

        public void DisableTech()
        {
            ProtoNode.state = RDTech.State.Unavailable;
            ResearchAndDevelopment.Instance.SetTechState(TechID, ProtoNode);
        }

        public void EnableTech()
        {
            ProtoNode.state = RDTech.State.Available;
            ResearchAndDevelopment.Instance.SetTechState(TechID, ProtoNode);
        }

        public bool IsInList()
        {
            return KCTGameStates.TechList.FirstOrDefault(t => t.TechID == TechID) != null;
        }

        public string GetItemName() => TechName;

        public double GetBuildRate() => BuildRate;

        public double GetTimeLeft() => TimeLeft;
        public double GetTimeLeftEst(double offset)
        {
            if (BuildRate > 0)
            {
                return TimeLeft;
            }
            else
            {
                double rate = Formula.GetResearchRate(ScienceCost, 0, 0) * Utilities.GetResearcherEfficiencyMultipliers();
                if (offset == 0d)
                    rate *= YearBasedRateMult;
                else
                    rate *= CalculateYearBasedRateMult(offset);
                return (ScienceCost - Progress) / rate;
            }
        }

        public BuildListVessel.ListType GetListType() => BuildListVessel.ListType.TechNode;

        public bool IsComplete() => Progress >= ScienceCost;

        public double IncrementProgress(double UTDiff)
        {
            // Don't progress blocked items
            if (GetBlockingTech(KCTGameStates.TechList) != null)
                return 0d;

            double bR = BuildRate;
            if (bR == 0d && PresetManager.Instance.ActivePreset.GeneralSettings.TechUnlockTimes)
                return 0d;

            double toGo = ScienceCost - Progress;
            double increment = bR * UTDiff;
            Progress += increment;
            if (IsComplete() || !PresetManager.Instance.ActivePreset.GeneralSettings.TechUnlockTimes)
            {
                if (ProtoNode == null) return 0d;
                EnableTech();

                try
                {
                    KCTEvents.OnTechCompleted.Fire(this);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                KCTGameStates.TechList.Remove(this);

                KCTGameStates.RecalculateBuildRates(); // this might change other rates

                double portion = toGo / increment;
                RP0.UnlockSubsidyHandler.Instance.IncrementSubsidyTime(TechID, portion * UTDiff);
                return (1d - portion) * UTDiff;
            }

            RP0.UnlockSubsidyHandler.Instance.IncrementSubsidyTime(TechID, UTDiff);
            return 0d;
        }

        public string GetBlockingTech(KCTObservableList<TechItem> techList)
        {
            string blockingTech = null;

            List<string> parentList;
            if (!KerbalConstructionTimeData.techNameToParents.TryGetValue(TechID, out parentList))
            {
                Debug.LogError($"[KCT] Could not find techToParent for tech {TechID}");
                return null;
            }

            foreach (var t in techList)
            {
                if (parentList != null && parentList.Contains(t.TechID))
                {
                    blockingTech = t.TechName;
                    break;
                }
            }

            return blockingTech;
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
