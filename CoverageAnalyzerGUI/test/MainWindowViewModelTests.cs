using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CoverageAnalyzerGUI.ViewModels;
using CoverageAnalyzerGUI.Commands;

namespace CoverageAnalyzerGUI.Tests
{
    [TestClass]
    public class MainWindowViewModelTests
    {
        [TestMethod]
        public void MainWindowViewModel_ShouldInitialize_WithDefaultValues()
        {
            // Arrange & Act
            var viewModel = new MainWindowViewModel();

            // Assert
            Assert.AreEqual("Ready", viewModel.StatusText);
            Assert.AreEqual("Not Loaded", viewModel.ParserStatus);
            Assert.AreEqual(0, viewModel.FileCount);
            Assert.IsNotNull(viewModel.NewProjectCommand);
            Assert.IsNotNull(viewModel.OpenProjectCommand);
            Assert.IsNotNull(viewModel.RunCoverageAnalysisCommand);
        }

        [TestMethod]
        public void RelayCommand_ShouldExecute_WhenCalled()
        {
            // Arrange
            bool executed = false;
            var command = new RelayCommand(() => executed = true);

            // Act
            command.Execute(null);

            // Assert
            Assert.IsTrue(executed);
        }

        [TestMethod]
        public void RelayCommand_ShouldCheckCanExecute_WhenSpecified()
        {
            // Arrange
            bool canExecute = false;
            var command = new RelayCommand(() => { }, () => canExecute);

            // Act & Assert
            Assert.IsFalse(command.CanExecute(null));

            canExecute = true;
            Assert.IsTrue(command.CanExecute(null));
        }

        [TestMethod]
        public void StatusText_ShouldNotifyPropertyChanged_WhenSet()
        {
            // Arrange
            var viewModel = new MainWindowViewModel();
            bool propertyChanged = false;
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(viewModel.StatusText))
                    propertyChanged = true;
            };

            // Act
            viewModel.StatusText = "New Status";

            // Assert
            Assert.IsTrue(propertyChanged);
            Assert.AreEqual("New Status", viewModel.StatusText);
        }
    }
}