using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CoverageAnalyzerGUI.Models;
using CoverageAnalyzerGUI.Interop;

namespace CoverageAnalyzerGUI.Services
{
    public class CoverageParserService
    {
        private bool _isInitialized = false;

        public bool InitializeParsers(string coverageDataDirectory, string functionalDataDirectory)
        {
            try
            {
                // For now, we'll just check if the directories exist and contain hierarchy.txt files
                // The actual DLL initialization will be done when parsing specific files
                bool coverageExists = File.Exists(Path.Combine(coverageDataDirectory, "hierarchy.txt"));
                bool functionalExists = File.Exists(Path.Combine(functionalDataDirectory, "hierarchy.txt"));

                _isInitialized = coverageExists && functionalExists;
                return _isInitialized;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing parsers: {ex.Message}");
                return false;
            }
        }

        public CoverageData? GetCoverageHierarchy()
        {
            if (!_isInitialized)
                return null;

            try
            {
                // Use the actual CoverageParser DLL to parse hierarchy file
                string hierarchyFile = @"C:\Users\byaran\OneDrive - Advanced Micro Devices Inc\programming\coverage_analyzer\CoverageParser\data\code\hierarchy.txt";
                
                if (File.Exists(hierarchyFile))
                {
                    IntPtr data = CoverageParserWrapper.ParseHierarchyFile(hierarchyFile);
                    if (data != IntPtr.Zero)
                    {
                        try
                        {
                            var rootNode = ParseHierarchyFromDLL(data);
                            
                            return new CoverageData
                            {
                                RootNode = rootNode,
                                AllNodes = FlattenHierarchy(rootNode),
                                SourceFile = hierarchyFile,
                                ParsedAt = DateTime.Now
                            };
                        }
                        finally
                        {
                            CoverageParserWrapper.FreeHierarchyData(data);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting coverage hierarchy: {ex.Message}");
            }

            return null;
        }

        public FunctionalCoverageData? GetFunctionalCoverage()
        {
            if (!_isInitialized)
                return null;

            try
            {
                // Use the actual FunctionalParser DLL to parse hierarchy file
                string hierarchyFile = @"C:\Users\byaran\OneDrive - Advanced Micro Devices Inc\programming\coverage_analyzer\FunctionalCoverageParsers\hierarchy.txt";
                
                if (File.Exists(hierarchyFile))
                {
                    IntPtr data = FunctionalParserWrapper.ParseFunctionalFile(hierarchyFile);
                    if (data != IntPtr.Zero)
                    {
                        try
                        {
                            var functionalData = ParseFunctionalFromDLL(data);
                            return functionalData;
                        }
                        finally
                        {
                            FunctionalParserWrapper.FreeFunctionalData(data);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting functional coverage: {ex.Message}");
            }

            return null;
        }

        private HierarchyNode CreateMockHierarchyFromRealData()
        {
            // Based on the actual hierarchy.txt file structure we observed
            var tb = new HierarchyNode
            {
                Name = "tb",
                FullPath = "tb",
                Score = 61.56,
                LineScore = 93.33,
                CondScore = 48.75,
                ToggleScore = 55.08,
                FsmScore = 49.08,
                Level = 0
            };

            var gpu0 = new HierarchyNode
            {
                Name = "gpu0",
                FullPath = "tb.gpu0",
                Score = 61.56,
                LineScore = 93.33,
                CondScore = 48.75,
                ToggleScore = 55.08,
                FsmScore = 49.08,
                Level = 1,
                Parent = tb
            };

            var chip0 = new HierarchyNode
            {
                Name = "chip0",
                FullPath = "tb.gpu0.chip0",
                Score = 61.56,
                LineScore = 93.33,
                CondScore = 48.75,
                ToggleScore = 55.08,
                FsmScore = 49.08,
                Level = 2,
                Parent = gpu0
            };

            var core = new HierarchyNode
            {
                Name = "core",
                FullPath = "tb.gpu0.chip0.core",
                Score = 61.56,
                LineScore = 93.33,
                CondScore = 48.75,
                ToggleScore = 55.08,
                FsmScore = 49.08,
                Level = 3,
                Parent = chip0
            };

            var dftScanMarker = new HierarchyNode
            {
                Name = "dft_scan_marker",
                FullPath = "tb.gpu0.chip0.core.dft_scan_marker",
                Score = 0.00,
                LineScore = 0.00,
                CondScore = 0.00,
                ToggleScore = 0.00,
                FsmScore = 0.00,
                Level = 4,
                Parent = core
            };

            var riommuWrapper = new HierarchyNode
            {
                Name = "riommu_wrapper",
                FullPath = "tb.gpu0.chip0.core.riommu_wrapper",
                Score = 31.41,
                LineScore = 52.65,
                CondScore = 28.53,
                ToggleScore = 21.73,
                FsmScore = 22.75,
                Level = 4,
                Parent = core
            };

            var remoteSmuRiommu = new HierarchyNode
            {
                Name = "remote_smu_riommu",
                FullPath = "tb.gpu0.chip0.core.riommu_wrapper.remote_smu_riommu",
                Score = 22.02,
                LineScore = 60.08,
                CondScore = 16.43,
                ToggleScore = 5.11,
                FsmScore = 6.45,
                Level = 5,
                Parent = riommuWrapper
            };

            // Build the hierarchy
            core.Children.AddRange(new[] { dftScanMarker, riommuWrapper });
            riommuWrapper.Children.Add(remoteSmuRiommu);
            chip0.Children.Add(core);
            gpu0.Children.Add(chip0);
            tb.Children.Add(gpu0);

            return tb;
        }

        private FunctionalCoverageData CreateMockFunctionalDataFromRealData()
        {
            // Based on the functional hierarchy.txt structure we observed
            var dcecGroup = new CoverageGroup
            {
                Name = "dcec_dc",
                FullPath = "dcec_dc",
                Coverage = 0.0 // Most entries showed 0.00 coverage
            };

            var dchubGroup = new CoverageGroup
            {
                Name = "dchubbubl",
                FullPath = "dcec_dc.dchubbubl",
                Coverage = 0.0,
                SubGroups = new List<CoverageGroup>()
            };

            var retPathGroup = new CoverageGroup
            {
                Name = "uRET_PATH",
                FullPath = "dcec_dc.dchubbubl.udchubbubl.uRET_PATH",
                Coverage = 0.0
            };

            // Add coverage points from the actual data
            var metafifoPoints = new List<CoveragePoint>();
            for (int i = 0; i <= 17; i++)
            {
                metafifoPoints.Add(new CoveragePoint
                {
                    Name = $"mem_{i}_0.PDP",
                    GroupPath = "dcec_dc.dchubbubl.udchubbubl.uRET_PATH.udchubbub_ret_path_compbuf.udchubbub_ret_path_compbuf_metafifo.umetafifo_ram",
                    Coverage = 0.0,
                    HitCount = 0,
                    TotalCount = 66
                });
            }

            retPathGroup.Points = metafifoPoints;
            dchubGroup.SubGroups.Add(retPathGroup);
            dcecGroup.SubGroups.Add(dchubGroup);

            return new FunctionalCoverageData
            {
                CoverageGroups = new List<CoverageGroup> { dcecGroup },
                CoveragePoints = metafifoPoints,
                OverallCoverage = 0.0,
                SourceFile = "FunctionalParser/functional/hierarchy.txt",
                ParsedAt = DateTime.Now
            };
        }

        private List<HierarchyNode> FlattenHierarchy(HierarchyNode root)
        {
            var result = new List<HierarchyNode>();
            FlattenHierarchyRecursive(root, result);
            return result;
        }

        private void FlattenHierarchyRecursive(HierarchyNode node, List<HierarchyNode> result)
        {
            result.Add(node);
            foreach (var child in node.Children)
            {
                FlattenHierarchyRecursive(child, result);
            }
        }

        private HierarchyNode ParseHierarchyFromDLL(IntPtr data)
        {
            // Parse hierarchy data from the DLL
            int nodeCount = CoverageParserWrapper.GetNodeCount(data);
            
            // Create root node
            var rootNode = new HierarchyNode
            {
                Name = "Coverage Root",
                FullPath = "/",
                Score = 0.0,
                LineScore = 0.0,
                CondScore = 0.0,
                ToggleScore = 0.0,
                FsmScore = 0.0,
                Level = 0,
                Children = new List<HierarchyNode>()
            };

            // Parse all nodes from DLL
            for (int i = 0; i < nodeCount; i++)
            {
                var nodeName = CoverageParserWrapper.PtrToString(CoverageParserWrapper.GetNodeName(data, i));
                var nodePath = CoverageParserWrapper.PtrToString(CoverageParserWrapper.GetNodePath(data, i));
                var nodeType = CoverageParserWrapper.GetNodeType(data, i);
                var coveragePercent = CoverageParserWrapper.GetCoveragePercentage(data, i);
                var hitCount = CoverageParserWrapper.GetHitCount(data, i);
                var totalCount = CoverageParserWrapper.GetTotalCount(data, i);

                var node = new HierarchyNode
                {
                    Name = nodeName,
                    FullPath = nodePath,
                    Score = coveragePercent,
                    LineScore = coveragePercent,
                    CondScore = 0.0,
                    ToggleScore = 0.0,
                    FsmScore = 0.0,
                    Level = 1,
                    Children = new List<HierarchyNode>()
                };

                rootNode.Children.Add(node);
            }

            return rootNode;
        }

        private FunctionalCoverageData ParseFunctionalFromDLL(IntPtr data)
        {
            // Parse functional coverage data from the DLL
            int functionalCount = FunctionalParserWrapper.GetFunctionalCount(data);
            
            var coverageGroups = new List<CoverageGroup>();
            var coveragePoints = new List<CoveragePoint>();

            for (int i = 0; i < functionalCount; i++)
            {
                var name = FunctionalParserWrapper.PtrToString(FunctionalParserWrapper.GetFunctionalName(data, i));
                var description = FunctionalParserWrapper.PtrToString(FunctionalParserWrapper.GetFunctionalDescription(data, i));
                var status = FunctionalParserWrapper.GetFunctionalStatus(data, i);
                var weight = FunctionalParserWrapper.GetFunctionalWeight(data, i);
                var category = FunctionalParserWrapper.PtrToString(FunctionalParserWrapper.GetFunctionalCategory(data, i));

                var point = new CoveragePoint
                {
                    Name = name,
                    GroupPath = category,
                    Coverage = weight,
                    HitCount = status > 0 ? 1 : 0,
                    TotalCount = 1
                };

                coveragePoints.Add(point);
            }

            return new FunctionalCoverageData
            {
                CoverageGroups = coverageGroups,
                CoveragePoints = coveragePoints,
                OverallCoverage = coveragePoints.Count > 0 ? coveragePoints.Average(p => p.Coverage) : 0.0,
                SourceFile = "FunctionalParser/hierarchy.txt",
                ParsedAt = DateTime.Now
            };
        }

        public void Cleanup()
        {
            if (_isInitialized)
            {
                try
                {
                    // No specific cleanup needed for P/Invoke wrappers
                    // Memory management is handled by the FreeXXXData methods when called
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
                }
                finally
                {
                    _isInitialized = false;
                }
            }
        }
    }
}