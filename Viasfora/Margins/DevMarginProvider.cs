﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Winterdom.Viasfora.Margins {
  [Export(typeof(IWpfTextViewMarginProvider))]
  [Name("Viasfora Developer Margin")]
  [Order(Before = "Wpf Horizontal Scrollbar")]
  [MarginContainer("bottom")]
  [ContentType("text")]
  [TextViewRole(PredefinedTextViewRoles.Document)]
  public class DevMarginProvider : IWpfTextViewMarginProvider {
    public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer) {
      return new DevViewMargin(wpfTextViewHost);
    }
  }
  
}
