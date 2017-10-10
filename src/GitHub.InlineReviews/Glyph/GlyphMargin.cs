﻿using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using GitHub.InlineReviews.Glyph.Implementation;

namespace GitHub.InlineReviews.Glyph
{
    /// <summary>
    /// Responsibe for updating the margin when tags change.
    /// </summary>
    /// <typeparam name="TGlyphTag">The type of glyph tag we're managing.</typeparam>
    public sealed class GlyphMargin<TGlyphTag> : IWpfTextViewMargin, ITextViewMargin, IDisposable where TGlyphTag: ITag
    {
        bool handleZoom;
        bool isDisposed;
        Grid marginVisual;
        double marginWidth;
        bool refreshAllGlyphs;
        ITagAggregator<TGlyphTag> tagAggregator;
        IWpfTextView textView;
        string marginName;
        GlyphMarginVisualManager<TGlyphTag> visualManager;
        Func<ITextBuffer, bool> isMarginVisible;

        public GlyphMargin(
            IWpfTextViewHost wpfTextViewHost,
            IGlyphFactory<TGlyphTag> glyphFactory,
            Func<Grid> gridFactory,
            ITagAggregator<TGlyphTag> tagAggregator,
            IEditorFormatMap editorFormatMap,
            Func<ITextBuffer, bool> isMarginVisible,
            string marginPropertiesName, string marginName, bool handleZoom = true, double marginWidth = 17.0)
        {
            textView = wpfTextViewHost.TextView;
            this.tagAggregator = tagAggregator;
            this.isMarginVisible = isMarginVisible;
            this.marginName = marginName;
            this.handleZoom = handleZoom;
            this.marginWidth = marginWidth;

            marginVisual = gridFactory();
            marginVisual.Width = marginWidth;

            visualManager = new GlyphMarginVisualManager<TGlyphTag>(textView, glyphFactory, marginVisual,
                this, editorFormatMap, marginPropertiesName);

            // Do on Loaded to give diff view a chance to initialize.
            marginVisual.Loaded += OnLoaded;
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                tagAggregator.Dispose();
                marginVisual = null;
                isDisposed = true;
            }
        }

        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            return (marginName == this.marginName) ? this : null;
        }

        public bool Enabled
        {
            get
            {
                ThrowIfDisposed();
                return true;
            }
        }

        public double MarginSize
        {
            get
            {
                ThrowIfDisposed();
                return marginVisual.Width;
            }
        }

        public FrameworkElement VisualElement
        {
            get
            {
                ThrowIfDisposed();
                return marginVisual;
            }
        }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshMarginVisibility();

            tagAggregator.BatchedTagsChanged += OnBatchedTagsChanged;
            textView.LayoutChanged += OnLayoutChanged;
            if (handleZoom)
            {
                textView.ZoomLevelChanged += OnZoomLevelChanged;
            }

            if (textView.InLayout)
            {
                refreshAllGlyphs = true;
            }
            else
            {
                foreach (var line in textView.TextViewLines)
                {
                    RefreshGlyphsOver(line);
                }
            }

            if (handleZoom)
            {
                marginVisual.LayoutTransform = new ScaleTransform(textView.ZoomLevel / 100.0, textView.ZoomLevel / 100.0);
                marginVisual.LayoutTransform.Freeze();
            }
        }

        void OnBatchedTagsChanged(object sender, BatchedTagsChangedEventArgs e)
        {
            RefreshMarginVisibility();

            if (!textView.IsClosed)
            {
                var list = new List<SnapshotSpan>();
                foreach (var span in e.Spans)
                {
                    list.AddRange(span.GetSpans(textView.TextSnapshot));
                }

                if (list.Count > 0)
                {
                    var span = list[0];
                    int start = span.Start;
                    int end = span.End;
                    for (int i = 1; i < list.Count; i++)
                    {
                        span = list[i];
                        start = Math.Min(start, span.Start);
                        end = Math.Max(end, span.End);
                    }

                    var rangeSpan = new SnapshotSpan(textView.TextSnapshot, start, end - start);
                    visualManager.RemoveGlyphsByVisualSpan(rangeSpan);
                    foreach (var line in textView.TextViewLines.GetTextViewLinesIntersectingSpan(rangeSpan))
                    {
                        RefreshGlyphsOver(line);
                    }
                }
            }
        }

        void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            RefreshMarginVisibility();

            visualManager.SetSnapshotAndUpdate(textView.TextSnapshot, e.NewOrReformattedLines, e.VerticalTranslation ? (IList<ITextViewLine>)textView.TextViewLines : e.TranslatedLines);

            var lines = refreshAllGlyphs ? (IList<ITextViewLine>)textView.TextViewLines : e.NewOrReformattedLines;
            foreach (var line in lines)
            {
                visualManager.RemoveGlyphsByVisualSpan(line.Extent);
                RefreshGlyphsOver(line);
            }

            refreshAllGlyphs = false;
        }

        void OnZoomLevelChanged(object sender, ZoomLevelChangedEventArgs e)
        {
            refreshAllGlyphs = true;
            marginVisual.LayoutTransform = e.ZoomTransform;
        }

        void RefreshGlyphsOver(ITextViewLine textViewLine)
        {
            foreach (IMappingTagSpan<TGlyphTag> span in tagAggregator.GetTags(textViewLine.ExtentAsMappingSpan))
            {
                NormalizedSnapshotSpanCollection spans;
                if (span.Span.Start.GetPoint(textView.VisualSnapshot.TextBuffer, PositionAffinity.Predecessor).HasValue &&
                    ((spans = span.Span.GetSpans(textView.TextSnapshot)).Count > 0))
                {
                    visualManager.AddGlyph(span.Tag, spans[0]);
                }
            }
        }

        void RefreshMarginVisibility()
        {
            marginVisual.Visibility = isMarginVisible(textView.TextBuffer) ? Visibility.Visible : Visibility.Collapsed;
        }

        void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(marginName);
            }
        }
    }
}
