﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Winterdom.Viasfora.Text {

  class RainbowTagger : ITagger<ClassificationTag>, IDisposable {
    private ITextBuffer theBuffer;
    private ITextView theView;
    private ClassificationTag[] rainbowTags;
    private Dictionary<char, char> braceList = new Dictionary<char, char>();
    private const String BRACE_CHARS = "(){}[]";
    private const int MAX_DEPTH = 4;
    private List<ITagSpan<ClassificationTag>> braceTags = new List<ITagSpan<ClassificationTag>>();

#pragma warning disable 67
    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
#pragma warning restore 67

    internal RainbowTagger(
          ITextBuffer buffer, ITextView textView,
          IClassificationTypeRegistryService registry) {
      this.theView = textView;
      this.theBuffer = buffer;
      rainbowTags = new ClassificationTag[MAX_DEPTH];

      for ( int i = 0; i < MAX_DEPTH; i++ ) {
        rainbowTags[i] = new ClassificationTag(
          registry.GetClassificationType(Constants.RAINBOW + (i + 1)));
      }
      for ( int i = 0; i < BRACE_CHARS.Length; i += 2 ) {
        braceList.Add(BRACE_CHARS[i], BRACE_CHARS[i + 1]);
      }

      this.theBuffer.Changed += this.BufferChanged;
      this.theView.LayoutChanged += this.ViewLayoutChanged;
      VsfSettings.SettingsUpdated += this.OnSettingsUpdated;

      UpdateBraceList(new SnapshotPoint(buffer.CurrentSnapshot, 0));
    }

    public void Dispose() {
      if ( theBuffer != null ) {
        VsfSettings.SettingsUpdated -= OnSettingsUpdated;
        theView.LayoutChanged -= ViewLayoutChanged;
        theBuffer.Changed -= this.BufferChanged;
        theBuffer = null;
        theView = null;
      }
    }

    public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
      if ( !VsfSettings.RainbowTagsEnabled ) yield break;
      if ( spans.Count == 0 ) {
        yield break;
      }
      ITextSnapshot snapshot = spans[0].Snapshot;
      SnapshotPoint startPoint = new SnapshotPoint(snapshot, 0);
      foreach ( var tagSpan in braceTags ) {
        if ( tagSpan.Span.Snapshot != snapshot ) {
          yield return new TagSpan<ClassificationTag>(
            tagSpan.Span.TranslateTo(snapshot, SpanTrackingMode.EdgeExclusive),
            tagSpan.Tag);
        } else {
          yield return tagSpan;
        }
      }
    }

    private void UpdateBraceList(SnapshotPoint startPoint) {
      braceTags.Clear();
      ITextSnapshot snapshot = startPoint.Snapshot;

      BraceExtractor extractor =  new BraceExtractor(startPoint, BRACE_CHARS);
      var braces = extractor.All();
      foreach ( var tagSpan in LookForMatchingPairs(snapshot, braces) ) {
        braceTags.Add(tagSpan);
      }
    }

    class Pair {
      public char Brace { get; set; }
      public int Depth { get; set; }
      public int Open { get; set; }
    }

    private IEnumerable<ITagSpan<ClassificationTag>> LookForMatchingPairs(
          ITextSnapshot snapshot, IEnumerable<SnapshotPoint> braces) {
      Stack<Pair> pairs = new Stack<Pair>();

      foreach ( var pt in braces ) {
        char ch = pt.GetChar();
        if ( IsOpeningBrace(ch) ) {
          Pair p = new Pair {
            Brace = ch, Depth = pairs.Count,
            Open = pt.Position
          };
          pairs.Push(p);
          // yield opening brace
          var tag = this.rainbowTags[p.Depth % MAX_DEPTH];
          var span = new SnapshotSpan(snapshot, p.Open, 1);
          yield return new TagSpan<ClassificationTag>(span, tag);
        } else if ( IsClosingBrace(ch) && pairs.Count > 0 ) {
          Pair p = pairs.Peek();
          if ( braceList[p.Brace] == ch ) {
            // yield closing brace
            pairs.Pop();
            var tag = this.rainbowTags[p.Depth % MAX_DEPTH];
            var span = new SnapshotSpan(snapshot, pt.Position, 1);
            yield return new TagSpan<ClassificationTag>(span, tag);
          }
        }
      }
    }

    private bool IsClosingBrace(char ch) {
      return braceList.Values.Contains(ch);
    }

    private bool IsOpeningBrace(char ch) {
      return braceList.ContainsKey(ch);
    }

    void OnSettingsUpdated(object sender, EventArgs e) {
      UpdateTags(theBuffer.CurrentSnapshot, 0);
    }

    private void BufferChanged(object sender, TextContentChangedEventArgs e) {
      foreach ( var change in e.Changes ) {
        if ( TextContainsBrace(change.NewText) || TextContainsBrace(change.OldText) ) {
          UpdateBraceList(new SnapshotPoint(e.After, 0));
          UpdateTags(e.After, 0);
        }
      }
    }

    private void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) {
      if ( e.NewSnapshot != e.OldSnapshot ) {
        UpdateTags(e.NewSnapshot, 0);
      }
    }

    private bool TextContainsBrace(String change) {
      foreach ( char ch in BRACE_CHARS ) {
        if ( change.Contains(ch) )
          return true;
      }
      return false;
    }

    private void UpdateTags(ITextSnapshot snapshot, int startPosition) {
      var tempEvent = TagsChanged;
      if ( tempEvent != null ) {
        tempEvent(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, startPosition,
            snapshot.Length - startPosition)));
      }
    }
  }
}