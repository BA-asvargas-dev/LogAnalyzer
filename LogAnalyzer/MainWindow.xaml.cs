using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using static System.Runtime.InteropServices.JavaScript.JSType;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LogAnalyzer
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }

        private async void OnGenerateReports(object sender, RoutedEventArgs e)
        {
            string logPath = txtLogPath.Text;
            string appNamesText = txtAppNames.Text;
            string outputFolder = txtOutputFolder.Text;

            if (string.IsNullOrWhiteSpace(logPath) || string.IsNullOrWhiteSpace(appNamesText) || string.IsNullOrWhiteSpace(outputFolder))
            {
                await ShowMessage("Error", "Por favor, complete todos los campos antes de generar los informes.");
                return;
            }

            var appNames = appNamesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(a => a.Trim())
                                        .ToArray();

            if (appNames.Length == 0)
            {
                await ShowMessage("Error", "No se detectaron aplicaciones válidas en la lista.");
                return;
            }

            progressBar.Visibility = Visibility.Visible;
            progressBar.Value = 0;
            progressBar.Maximum = appNames.Length;
            txtLog.Text = "";
            string? appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (appPath == null)
            {
                await ShowMessage("Error", "No se pudo determinar la ruta de la aplicación.");
                return;
            }

            string logParserPath = Path.Combine(appPath, "tools", "LogParser.exe");
            List<string> appsWithErrors = new List<string>(); // Lista para aplicaciones con errores

            for (int i = 0; i < appNames.Length; i++)
            {
                string appName = appNames[i];
                AppendLog($"Generando informe para la aplicación: {appName}...");

                string safeAppName = new string(appName
                    .Replace(' ', '_')
                    .Normalize(NormalizationForm.FormD)
                    .Where(c => !char.IsWhiteSpace(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    .ToArray());

                if (!safeAppName.EndsWith(".csv"))
                {
                    safeAppName += ".csv";
                }

                string outputFile = Path.Combine(outputFolder, $"resultados_{safeAppName}");
                string logParserCommand = $"{logParserPath} \"SELECT date AS Fecha, time AS Hora, s-sitename AS Sitio, s-ip AS IP_Servidor, " +
                                           $"cs-method AS Metodo, cs-uri-stem AS Ruta, cs-uri-query AS Consulta_Ruta, s-port AS Puerto_Servidor, " +
                                           $"cs-username AS Usuario, c-ip AS IP_Cliente, cs(User-Agent) AS UserAgent, " +
                                           $"sc-status AS Estado_Respuesta, sc-substatus AS Subestado_Respuesta, " +
                                           $"sc-win32-status AS Estado_Win32 FROM {logPath}/*.log WHERE cs-uri-stem LIKE '%{appName}%'\" " +
                                           $"-i:IISW3C -o:CSV > {outputFile}";

                string errorFilePath = Path.Combine(Path.GetTempPath(), "logparser_error.txt");
                string command = $"{logParserCommand} 2> {errorFilePath}";

                try
                {
                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C {logParserCommand}",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using (var process = Process.Start(processStartInfo))
                    {
                        if (process == null)
                        {
                            AppendLog($"Error al iniciar LogParser para {appName}");
                            appsWithErrors.Add(appName);
                            continue;
                        }

                        process.WaitForExit(); // Asegura que LogParser termine antes de continuar
                        string output = await process.StandardOutput.ReadToEndAsync();
                        string errors = await process.StandardError.ReadToEndAsync();
                        await process.WaitForExitAsync();

                        AppendLog(output);
                        if (!string.IsNullOrWhiteSpace(errors))
                        {
                            AppendLog($"Errores: {errors}");
                            appsWithErrors.Add(appName);
                        }

                        if (File.Exists(outputFile))
                        {
                            AppendLog($"Informe generado exitosamente para {appName}. Guardado en: {outputFile}");
                        }
                        else
                        {
                            AppendLog($"No se generó el informe para {appName}.");
                            appsWithErrors.Add(appName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"Error al ejecutar LogParser para {appName}: {ex.Message}");
                    appsWithErrors.Add(appName);
                }

                progressBar.Value = i + 1;
                await Task.Delay(50);
            }

            AppendLog("Todos los reportes han sido generados.");
            progressBar.Visibility = Visibility.Collapsed;

            // Verificar si hubo errores y mostrar el mensaje correspondiente
            string message = appsWithErrors.Any()
                ? $"Hubo errores al generar los informes para las siguientes aplicaciones: {string.Join(", ", appsWithErrors)}."
                : "Todos los informes se generaron exitosamente.";

            await ShowMessage("Proceso Completo", message);

            // Limpiar la pantalla
            ClearScreen();
        }

        private void ClearScreen()
        {
            txtLog.Text = "";
            txtAppNames.Text = "";
            txtLogPath.Text = "";
            txtOutputFolder.Text = "";
            progressBar.Value = 0;
        }

        private async Task ShowMessage(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private void AppendLog(string message)
        {
            txtLog.Text += message + Environment.NewLine;
            txtLog.Select(txtLog.Text.Length, 0);
            txtLog.Focus(FocusState.Programmatic);
        }

    }
}

