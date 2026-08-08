using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Hornwatch.Core.Navigation;
using Lumina.Excel.Sheets;

namespace Hornwatch.Navigation;

public sealed class SelectYesnoConfirmer(IGameGui gameGui, IDataManager data, IPluginLog log) : IConfirmationDialog
{
    private const string AddonName = "SelectYesno";

    private const int YesIndex = 0;

    private const uint RecallPromptRow = 197;

    private static readonly char[] TemplateSeparators = ['\n', '\r'];

    private const int ShortestUsableFragment = 8;

    private bool answeredCurrent;
    private bool reportedMismatch;
    private string? promptWording;

    public unsafe bool IsAwaitingAnswer => Visible() != null;

    public unsafe void Accept()
    {
        var addon = Locate();

        if (addon == null)
        {
            answeredCurrent = false;
            return;
        }

        if (answeredCurrent)
        {
            return;
        }

        answeredCurrent = true;
        reportedMismatch = false;

        var choice = stackalloc AtkValue[1];
        choice->Type = AtkValueType.Int;
        choice->Int = YesIndex;
        addon->AtkUnitBase.FireCallback(1, choice);
    }

    private unsafe AddonSelectYesno* Visible()
    {
        var addon = (AddonSelectYesno*)gameGui.GetAddonByName(AddonName).Address;

        if (addon == null || !addon->AtkUnitBase.IsVisible ||
            addon->AtkUnitBase.UldManager.LoadedState != AtkLoadState.Loaded)
        {
            return null;
        }

        return addon;
    }

    private unsafe AddonSelectYesno* Locate()
    {
        var addon = Visible();

        if (addon == null)
        {
            return null;
        }

        if (CarriesRecallPrompt(addon))
        {
            return addon;
        }

        if (!reportedMismatch)
        {
            reportedMismatch = true;
            log.Information($"[recall] leaving a SelectYesno alone, no recall prompt in: {DescribeTexts(addon)}");
        }

        return null;
    }

    private unsafe bool CarriesRecallPrompt(AddonSelectYesno* addon)
    {
        foreach (var text in ReadTexts(addon))
        {
            if (IsRecallPrompt(text))
            {
                return true;
            }
        }

        return false;
    }

    private static unsafe List<string> ReadTexts(AddonSelectYesno* addon)
    {
        var texts = new List<string>();
        var values = addon->AtkUnitBase.AtkValues;
        if (values == null)
        {
            return texts;
        }

        for (var i = 0; i < addon->AtkUnitBase.AtkValuesCount; i++)
        {
            if (values[i].Type == AtkValueType.String && values[i].String.Value != null)
            {
                texts.Add(values[i].String.ToString());
            }
        }

        return texts;
    }

    private static unsafe string DescribeTexts(AddonSelectYesno* addon) =>
        string.Join(" | ", ReadTexts(addon));

    private bool IsRecallPrompt(string prompt)
    {
        var trimmed = prompt.Trim();
        if (trimmed.Length < ShortestUsableFragment)
        {
            return false;
        }

        var wording = promptWording ??= BuildWording();
        if (wording.Length == 0)
        {
            return false;
        }

        return trimmed.Contains(wording, StringComparison.OrdinalIgnoreCase)
               || wording.Contains(trimmed, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildWording()
    {
        var template = data.GetExcelSheet<Addon>()?.GetRowOrDefault(RecallPromptRow)?.Text.ExtractText();
        if (string.IsNullOrWhiteSpace(template))
        {
            log.Warning($"Addon row {RecallPromptRow} is empty; the recall confirmation cannot be verified.");
            return string.Empty;
        }

        var longest = string.Empty;
        foreach (var piece in template.Split(TemplateSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = piece.Trim();
            if (trimmed.Length > longest.Length)
            {
                longest = trimmed;
            }
        }

        if (longest.Length < ShortestUsableFragment)
        {
            log.Warning($"Addon row {RecallPromptRow} ('{template}') has no wording long enough to match against.");
            return string.Empty;
        }

        log.Information($"[recall] confirming dialogs whose text matches '{longest}'.");
        return longest;
    }
}
