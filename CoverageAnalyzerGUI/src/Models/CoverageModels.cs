namespace CoverageAnalyzerGUI.Models
{
    public class HierarchyNode
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public double Score { get; set; }
        public double LineScore { get; set; }
        public double CondScore { get; set; }
        public double ToggleScore { get; set; }
        public double FsmScore { get; set; }
        public int Level { get; set; }
        public List<HierarchyNode> Children { get; set; } = new List<HierarchyNode>();
        public HierarchyNode? Parent { get; set; }

        public string DisplayText => $"{Name} ({Score:F2}%)";
        public bool HasChildren => Children.Count > 0;
    }

    public class CoverageData
    {
        public HierarchyNode? RootNode { get; set; }
        public List<HierarchyNode> AllNodes { get; set; } = new List<HierarchyNode>();
        public string SourceFile { get; set; } = string.Empty;
        public DateTime ParsedAt { get; set; } = DateTime.Now;
    }

    public class FunctionalCoverageData
    {
        public List<CoverageGroup> CoverageGroups { get; set; } = new List<CoverageGroup>();
        public List<CoveragePoint> CoveragePoints { get; set; } = new List<CoveragePoint>();
        public double OverallCoverage { get; set; }
        public string SourceFile { get; set; } = string.Empty;
        public DateTime ParsedAt { get; set; } = DateTime.Now;
    }

    public class CoverageGroup
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public double Coverage { get; set; }
        public List<CoveragePoint> Points { get; set; } = new List<CoveragePoint>();
        public List<CoverageGroup> SubGroups { get; set; } = new List<CoverageGroup>();

        public string DisplayText => $"{Name} ({Coverage:F1}%)";
    }

    public class CoveragePoint
    {
        public string Name { get; set; } = string.Empty;
        public string GroupPath { get; set; } = string.Empty;
        public double Coverage { get; set; }
        public int HitCount { get; set; }
        public int TotalCount { get; set; }
        public bool IsCovered => Coverage > 0;

        public string DisplayText => $"{Name} ({HitCount}/{TotalCount})";
    }
}