﻿using System;
using System.Runtime.InteropServices;
using Winterdom.Viasfora.Design;
using Winterdom.Viasfora.Util;

namespace Winterdom.Viasfora.Options {
  [Guid(Guids.TextObfuscationOptions)]
  public class TextObfuscationOptionsPage : UIElementDialogPage {
    private TextObfuscationDialog dialog;
    protected override System.Windows.UIElement Child {
      get { return dialog; }
    }
    public TextObfuscationOptionsPage() {
     this.dialog = new TextObfuscationDialog();
    }
    public override void SaveSettingsToStorage() {
      base.SaveSettingsToStorage();

      // This call is necessary so that if the grid has the focus
      // it loses it so that changes to the data context are
      // propagated properly!
      MoveFocusToNext();

      var settings = SettingsContext.GetSettings();
      settings.TextObfuscationRegexes = dialog.Entries.ListToJson();
      settings.Save();
    }
    public override void LoadSettingsFromStorage() {
      base.LoadSettingsFromStorage();
      var settings = SettingsContext.GetSettings();

      this.dialog.Entries.Clear();
      var entries = settings.TextObfuscationRegexes.ListFromJson<RegexEntry>();
      foreach ( var entry in entries ) {
        this.dialog.Entries.Add(entry);
      }
    }
  }
}