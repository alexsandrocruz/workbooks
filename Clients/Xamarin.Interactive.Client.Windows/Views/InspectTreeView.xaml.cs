//
// Author:
//   Larry Ewing <lewing@xamarin.com>
//
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Xamarin.Interactive.Client.ViewInspector;
using Xamarin.Interactive.Client.Windows.ViewModels;
using Xamarin.Interactive.Remote;

namespace Xamarin.Interactive.Client.Windows.Views
{
    partial class InspectTreeView : UserControl
    {
        InspectTreeRoot Tree;
        InspectTreeNode3D selectedNode;
        InspectTreeNode3D representedNode;
        Point downPosition;

        public InspectTreeView ()
        {
            InitializeComponent ();

            CaptureBorder.MouseMove += HandleMouseMove;
            CaptureBorder.MouseDown += HandleMouseDown;
            CaptureBorder.MouseUp += HandleMouseUp;
        }

        void HandleMouseDown (object sender, MouseButtonEventArgs e)
        {
            downPosition = e.GetPosition (CaptureBorder);
        }

        void HandleMouseUp (object sender, MouseButtonEventArgs e)
        {
            var currentPosition = e.GetPosition (CaptureBorder);
            var offset = currentPosition - downPosition;
            if (offset.X == 0 && offset.Y == 0) {
                SelectedNode = HitTest (currentPosition);
            }
        }

        void HandleMouseMove (object sender, MouseEventArgs e)
        {
            var currentPosition = e.GetPosition (CaptureBorder);
            HoverNode = HitTest (currentPosition);
        }

        InspectTreeNode3D HitTest (Point position)
        {
            var hitParams = new PointHitTestParameters (position);
            HitNode = null;
            VisualTreeHelper.HitTest (viewport, null, ResultCallback, hitParams);
            return HitNode;
        }

        public HitTestResultBehavior ResultCallback (HitTestResult result)
        {
            var meshResult = result as RayMeshGeometry3DHitTestResult;

            if (meshResult != null) {
                var node = meshResult.VisualHit as InspectTreeNode3D;
                if (!node.IsHitTestVisible ()) {
                    HitNode = null;
                    return HitTestResultBehavior.Continue;
                }
                HitNode = node;
                return HitTestResultBehavior.Stop;
            }

            HitNode = null;
            return HitTestResultBehavior.Continue;
        }

        void OnLoaded (object sender, RoutedEventArgs e)
        {
            // Viewport3Ds only raise events when the mouse is over the rendered 3D geometry.
            // In order to capture events whenever the mouse is over the client are we use a
            // same sized transparent Border positioned on top of the Viewport3D.
            if (Trackball != null)
                Trackball.EventSource = CaptureBorder;
        }

        InspectTreeNode3D HitNode { get; set; }

        InspectTreeNode3D hoverNode;
        InspectTreeNode3D HoverNode {
            get => hoverNode;
            set {
                if (value == hoverNode)
                    return;

                if (hoverNode != null)
                    hoverNode.Node.IsMouseOver = false;
            
                hoverNode = value;

                if (hoverNode != null)
                    hoverNode.Node.IsMouseOver = true;
            }
        }

        InspectTreeNode3D SelectedNode {
            get { return selectedNode; }
            set {
                if (value == selectedNode)
                    return;

                if (selectedNode != null)
                    selectedNode.Node.IsSelected = false;

                selectedNode = value;

                if (selectedNode != null)
                    selectedNode.Node.IsSelected = true;

                CurrentView.SelectedNode = selectedNode?.Node;
            }
        }

        internal DisplayMode DisplayMode {
            get { return (DisplayMode)GetValue (DisplayModeProperty); }
            set { SetValue (DisplayModeProperty, value); }
        }

        internal bool ShowHidden {
            get { return (bool)GetValue (ShowHiddenProperty); }
            set { SetValue (ShowHiddenProperty, value); }
        }

        public static readonly DependencyProperty DisplayModeProperty =
            DependencyProperty.Register (
                nameof (DisplayMode),
                typeof (DisplayMode),
                typeof (InspectTreeView),
                new PropertyMetadata (
                    DisplayMode.FramesAndContent,
                    new PropertyChangedCallback (DisplayModeValueChanged)));

        public static readonly DependencyProperty ShowHiddenProperty =
            DependencyProperty.Register (
                nameof (ShowHidden),
                typeof (bool),
                typeof (InspectTreeView),
                new PropertyMetadata (
                    false,
                    new PropertyChangedCallback (ShowHiddenValueChanged)));

        static void ShowHiddenValueChanged (DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
            => (dependencyObject as InspectTreeView)?.Rebuild ();

        static void DisplayModeValueChanged (DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
            => (dependencyObject as InspectTreeView)?.Rebuild ();

        public static readonly DependencyProperty CurrentViewProperty =
            DependencyProperty.Register (
                nameof (CurrentView),
                typeof (InspectTreeRoot),
                typeof (InspectTreeView),
                new PropertyMetadata (
                    null,
                    new PropertyChangedCallback (CurrentViewValueChanged)));

        private static void CurrentViewValueChanged (DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
        {
            var value = eventArgs.NewValue as InspectTreeRoot;
            var view = dependencyObject as InspectTreeView;

            void TreePropertyChanged (object sender, PropertyChangedEventArgs args)
            {
                var treeModel = sender as InspectTreeRoot;
                switch (args.PropertyName)
                {
                case nameof (InspectTreeRoot.RepresentedNode):
                    view.Rebuild ();
                    break;
                case nameof (InspectTreeRoot.SelectedNode):
                    view.UpdateSelected ();
                    break;
                }
            }

            if (view.Tree != null)
                   (view.Tree as INotifyPropertyChanged).PropertyChanged -= TreePropertyChanged;

            view.Tree = value;

            if (view.Tree != null)
                (view.Tree as INotifyPropertyChanged).PropertyChanged += TreePropertyChanged;

            view.Rebuild ();
        }

        void Rebuild ()
        {
            representedNode = null;
            selectedNode = null;
            topModel.Children.Clear ();
            if (Tree?.RepresentedNode != null) {
                var state = new InspectTreeState (DisplayMode, ShowHidden);
                var node3D = new InspectTreeNode3D (Tree.RepresentedNode, state);
                Tree.RepresentedNode.Build3D (node3D, state);
                representedNode = node3D;
                topModel.Children.Add (representedNode);
                UpdateSelected ();
            }
        }

        void UpdateSelected ()
        {
            if (Tree?.SelectedNode == null)
                SelectedNode = null;

            selectedNode = representedNode.TraverseTree (c => c.Children.OfType<InspectTreeNode3D> ())
                .FirstOrDefault (node3D => node3D.Node == Tree?.SelectedNode);
        }

        internal InspectTreeRoot CurrentView {
            get { return (InspectTreeRoot) GetValue (CurrentViewProperty); }
            set { SetValue (CurrentViewProperty, value); }
        }

        public static readonly DependencyProperty TrackballProperty =
            DependencyProperty.Register (
                nameof (Trackball),
                typeof (WpfDolly),
                typeof (InspectTreeView),
                new PropertyMetadata (
                    null,
                    new PropertyChangedCallback (TrackballValueChanged)));

        private static void TrackballValueChanged (DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
        {
            var value = eventArgs.NewValue as WpfDolly;
            var view = dependencyObject as InspectTreeView;

            value.EventSource = view.CaptureBorder;
        }

        internal WpfDolly Trackball {
            get { return (WpfDolly)GetValue (TrackballProperty); }
            set { SetValue (TrackballProperty, value); }
        }
    }
}
