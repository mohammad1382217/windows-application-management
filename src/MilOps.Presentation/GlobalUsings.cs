// Global usings for the Presentation project.
// NOTE: We deliberately do NOT add `using MilOps.Application;` globally because
// it would shadow `System.Windows.Application` (the WPF base class) at every
// call site of `Application.Current`. Files that need Application-layer types
// import them explicitly.
global using System.Windows;
global using MilOps.Presentation.Views;
global using Microsoft.Extensions.DependencyInjection;
