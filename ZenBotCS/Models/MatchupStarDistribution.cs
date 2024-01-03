using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZenBotCS.Models
{
    public class MatchupStarDistribution
    {
        public MatchupStarDistribution(int thFrom, int thTo)
        {
            ThFrom = thFrom;
            ThTo = thTo;
        }

        public int ZeroStar { get; set; }
        public int OneStar { get; set; }
        public int TwoStar { get; set; }
        public int ThreeStar { get; set; }
        public int ThFrom { get; set; }
        public int ThTo { get; set; }

        public string MatchupString => $"{ThFrom}vs{ThTo}";


        public int SumAttacks => ZeroStar + OneStar + TwoStar + ThreeStar;
        public double ZeroStarPercent => (double)ZeroStar / SumAttacks;
        public double OneStarPercent => (double)OneStar / SumAttacks;
        public double TwoStarPercent => (double)TwoStar / SumAttacks;
        public double ThreeStarPercent => (double)ThreeStar / SumAttacks;

    }
}
