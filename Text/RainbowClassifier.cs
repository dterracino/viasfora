﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Winterdom.Viasfora.Tags;
using System.Threading;

namespace Winterdom.Viasfora.Text {

  class RainbowClassifier : IClassifier, IDisposable {
    private ITextBuffer theBuffer;
    private IClassificationType[] rainbowTags;
    private object updateLock = new object();
    private Dispatcher dispatcher;
    private DispatcherTimer dispatcherTimer;
    private BraceCache braceCache;
    private int updatePendingFrom;

#pragma warning disable 67
    public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
#pragma warning restore 67

    internal RainbowClassifier(
          ITextBuffer buffer,
          IClassificationTypeRegistryService registry) {
      this.theBuffer = buffer;
      this.rainbowTags = GetRainbows(registry, Constants.MAX_RAINBOW_DEPTH);

      SetLanguage(buffer.ContentType);

      this.updatePendingFrom = -1;
      this.theBuffer.ChangedLowPriority += this.BufferChanged;
      this.theBuffer.ContentTypeChanged += this.ContentTypeChanged;
      VsfSettings.SettingsUpdated += this.OnSettingsUpdated;
      this.dispatcher = Dispatcher.CurrentDispatcher;

      UpdateBraceList(new SnapshotPoint(buffer.CurrentSnapshot, 0));
    }

    public static IClassificationType[] GetRainbows(IClassificationTypeRegistryService registry, int max) {
      var result = new IClassificationType[max];
      for ( int i = 0; i < max; i++ ) {
        result[i] = registry.GetClassificationType(Constants.RAINBOW + (i + 1));
      }
      return result;
    }

    public void Dispose() {
      if ( theBuffer != null ) {
        VsfSettings.SettingsUpdated -= OnSettingsUpdated;
        theBuffer.ChangedLowPriority -= this.BufferChanged;
        theBuffer.ContentTypeChanged -= this.ContentTypeChanged;
        theBuffer = null;
      }
    }

    public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
      List<ClassificationSpan> result = new List<ClassificationSpan>();
      if ( !VsfSettings.RainbowTagsEnabled ) {
        return result;
      }
      if ( span.Length == 0 ) {
        return result;
      }
      ITextSnapshot snapshot = span.Snapshot;
      if ( braceCache == null || braceCache.Snapshot != snapshot ) {
        return result;
      }
      foreach ( var brace in braceCache.BracesInSpans(new NormalizedSnapshotSpanCollection(span)) ) {
        var tag = rainbowTags[brace.Depth % Constants.MAX_RAINBOW_DEPTH];
        result.Add(brace.ToSpan(snapshot, tag));
      }
      return result;
    }

    private void UpdateBraceList(SnapshotPoint startPoint) {
      UpdateBraceList(startPoint, true);
    }

    private void UpdateBraceList(ITextSnapshot snapshot, INormalizedTextChangeCollection changes) {
      bool notify = true;
      var startPoint = new SnapshotPoint(snapshot, changes[0].NewSpan.Start);
      UpdateBraceList(startPoint, notify);
    }

    private void UpdateBraceList(SnapshotPoint startPoint, bool notifyUpdate) {
      this.braceCache.Invalidate(startPoint);
      SynchronousUpdate(notifyUpdate, startPoint);
    }

    private void SynchronousUpdate(bool notify, SnapshotPoint startPoint) {
      lock ( updateLock ) {
        // only invalidate the spans
        // containing all the positions of braces from the start point, leave
        // the rest alone
        if ( notify ) {
          this.updatePendingFrom = startPoint.Position;
          ScheduleUpdate();
        }
      }
    }

    private void ScheduleUpdate() {
      if ( theBuffer == null ) {
        return;
      }
      if ( dispatcherTimer == null ) {
        dispatcherTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, this.dispatcher);
        dispatcherTimer.Interval = TimeSpan.FromMilliseconds(500);
        dispatcherTimer.Tick += OnScheduledUpdate;
      }
      dispatcherTimer.Stop();
      dispatcherTimer.Start();
    }

    private void OnScheduledUpdate(object sender, EventArgs e) {
      if ( theBuffer == null ) return;
      try {
        dispatcherTimer.Stop();
        FireTagsChanged();
      } catch {
      }
    }

    private void FireTagsChanged() {
      var snapshot = braceCache.Snapshot;
      int upd = this.updatePendingFrom;
      var startPoint = new SnapshotPoint(snapshot, upd);
      foreach ( var brace in braceCache.BracesFromPosition(upd) ) {
        NotifyUpdateTags(new SnapshotSpan(snapshot, brace.Position, 1));
      }
      this.updatePendingFrom = -1;
    }

    private void SetLanguage(IContentType contentType) {
      this.braceCache = new BraceCache(this.theBuffer.CurrentSnapshot, contentType);
    }

    void OnSettingsUpdated(object sender, EventArgs e) {
      this.UpdateBraceList(new SnapshotPoint(this.theBuffer.CurrentSnapshot, 0));
    }

    private void BufferChanged(object sender, TextContentChangedEventArgs e) {
      if ( VsfSettings.RainbowTagsEnabled ) {
        // the snapshot changed, so we need to pretty much update
        // everything so that it matches.
        if ( e.Changes.Count > 0 ) {
          UpdateBraceList(e.After, e.Changes);
        }
      }
    }

    private void ContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
      if ( e.BeforeContentType.TypeName != e.AfterContentType.TypeName ) {
        this.SetLanguage(e.AfterContentType);
        this.UpdateBraceList(new SnapshotPoint(e.After, 0));
      }
    }

    private void NotifyUpdateTags(SnapshotSpan span) {
      var tempEvent = this.ClassificationChanged;
      if ( tempEvent != null ) {
        Action action = delegate() {
          tempEvent(this, new ClassificationChangedEventArgs(span));
        };
        dispatcher.BeginInvoke(action, DispatcherPriority.ApplicationIdle);
      }
    }
  }
}