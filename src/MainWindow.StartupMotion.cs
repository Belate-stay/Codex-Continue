using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CodexContinue;

public partial class MainWindow
{
    Storyboard? _startupTransition;
    bool _startupTransitionActive;

    FrameworkElement[] ConnectEntranceElements => new FrameworkElement[] { ConnectHeading, LoadDotsHost, ConnectMsg, SkipWrap };

    void BeginStartupTransition(bool animate)
    {
        var source = SplashTitle.TransformToAncestor(LayoutRoot).TransformBounds(new Rect(SplashTitle.RenderSize));
        double titleOpacity = SplashTitle.Opacity, buttonOpacity = BtnStart.Opacity;
        _startupTransitionActive = true;
        BtnStart.IsEnabled = false;
        System.Windows.Input.Keyboard.ClearFocus();

        ConnectPage.BeginAnimation(OpacityProperty, null);
        ConnectPage.Opacity = 1;
        ConnectPage.Visibility = Visibility.Visible;
        SplashPage.Background = Brushes.Transparent;
        foreach (var element in new UIElement[] { SplashTitle, SplashTitleShadow })
        {
            element.BeginAnimation(OpacityProperty, null);
            element.Opacity = 0;
            element.CacheMode = null;
        }
        BtnStart.BeginAnimation(OpacityProperty, null);
        BtnStart.Opacity = buttonOpacity;
        TitleName.Visibility = TitleIcon.Visibility = Visibility.Visible;
        TitleName.BeginAnimation(OpacityProperty, null);
        TitleName.Opacity = 0;
        TitleName.CacheMode = null;
        TitleName.RenderTransform = Transform.Identity;
        TitleIcon.Opacity = 0;
        LayoutRoot.UpdateLayout();
        var target = TitleName.TransformToAncestor(LayoutRoot).TransformBounds(new Rect(TitleName.RenderSize));

        if (!animate || source.IsEmpty || target.Width <= 0 || target.Height <= 0)
        {
            FinishStartupTransition();
            return;
        }

        // Keep the real header text in its layout slot; only its drawn position/size travels from the splash.
        var scale = new ScaleTransform(source.Width / target.Width, source.Height / target.Height);
        var move = new TranslateTransform(source.X - target.X, source.Y - target.Y);
        TitleName.RenderTransform = new TransformGroup { Children = { scale, move } };
        TitleName.Opacity = titleOpacity;
        var logoScale = new ScaleTransform(.9, .9);
        TitleIcon.RenderTransformOrigin = new Point(.5, .5);
        TitleIcon.RenderTransform = logoScale;
        LoadDots.BeginAnimation(OpacityProperty, null);
        LoadDots.Opacity = 1;

        var story = new Storyboard();
        Travel(TitleName, new PropertyPath("(0).(1)[0].(2)", RenderTransformProperty, TransformGroup.ChildrenProperty, ScaleTransform.ScaleXProperty), scale.ScaleX, 1);
        Travel(TitleName, new PropertyPath("(0).(1)[0].(2)", RenderTransformProperty, TransformGroup.ChildrenProperty, ScaleTransform.ScaleYProperty), scale.ScaleY, 1);
        Travel(TitleName, new PropertyPath("(0).(1)[1].(2)", RenderTransformProperty, TransformGroup.ChildrenProperty, TranslateTransform.XProperty), move.X, 0);
        Travel(TitleName, new PropertyPath("(0).(1)[1].(2)", RenderTransformProperty, TransformGroup.ChildrenProperty, TranslateTransform.YProperty), move.Y, 0);
        Add(TitleName, new PropertyPath(OpacityProperty), titleOpacity, 1, 180);
        Add(BtnStart, new PropertyPath(OpacityProperty), buttonOpacity, 0, 140);
        Add(TitleIcon, new PropertyPath(OpacityProperty), 0, 1, 280, 600);
        Add(TitleIcon, new PropertyPath("(0).(1)", RenderTransformProperty, ScaleTransform.ScaleXProperty), .9, 1, 280, 600);
        Add(TitleIcon, new PropertyPath("(0).(1)", RenderTransformProperty, ScaleTransform.ScaleYProperty), .9, 1, 280, 600);
        var delays = new[] { 620, 680, 740, 790 };
        var durations = new[] { 320, 340, 280, 240 };
        var elements = ConnectEntranceElements;
        for (int i = 0; i < elements.Length; i++)
        {
            var element = elements[i];
            element.BeginAnimation(OpacityProperty, null);
            element.Opacity = 0;
            var rise = new TranslateTransform(0, 6);
            element.RenderTransform = rise;
            Add(element, new PropertyPath(OpacityProperty), 0, 1, durations[i], delays[i]);
            Add(element, new PropertyPath("(0).(1)", RenderTransformProperty, TranslateTransform.YProperty), 6, 0, durations[i], delays[i]);
        }
        _startupTransition = story;
        story.Completed += (_, _) => { if (ReferenceEquals(_startupTransition, story)) FinishStartupTransition(); };
        Timeline.SetDesiredFrameRate(story, 240);
        story.Begin(this, HandoffBehavior.SnapshotAndReplace, isControllable: true);

        void Add(DependencyObject element, PropertyPath property, double from, double to, int duration, int delay = 0)
        {
            var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(duration))
            {
                BeginTime = TimeSpan.FromMilliseconds(delay),
                EasingFunction = Motion.Glide,
            };
            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, property);
            story.Children.Add(animation);
        }

        void Travel(DependencyObject element, PropertyPath property, double from, double to)
        {
            var animation = new DoubleAnimationUsingKeyFrames();
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            // Match the acceleration's ending speed to a short landing segment so the word does not slam into the header.
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(from + (to - from) * .944, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(640)))
                { EasingFunction = Motion.AccelerateOff });
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(to, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(720)))
                { EasingFunction = Motion.DecelerateMax });
            Storyboard.SetTarget(animation, element);
            Storyboard.SetTargetProperty(animation, property);
            story.Children.Add(animation);
        }
    }

    void FinishStartupTransition(bool preserveContent = false)
    {
        if (!_startupTransitionActive) return;
        _startupTransitionActive = false;
        var elements = ConnectEntranceElements;
        var poses = preserveContent ? elements.Select(e => (e.Opacity, e.RenderTransform.Value)).ToArray() : null;
        var story = _startupTransition;
        _startupTransition = null;
        story?.Remove(this);
        TitleName.RenderTransform = TitleIcon.RenderTransform = Transform.Identity;
        TitleName.Opacity = TitleIcon.Opacity = 1;
        TitleName.Visibility = TitleIcon.Visibility = Visibility.Visible;
        SplashPage.Visibility = Visibility.Collapsed;
        SplashPage.SetResourceReference(System.Windows.Controls.Panel.BackgroundProperty, "PageCanvasBrush");
        BtnStart.Opacity = 1;
        BtnStart.IsEnabled = true;
        for (int i = 0; i < elements.Length; i++)
        {
            elements[i].Opacity = poses == null ? 1 : poses[i].Opacity;
            elements[i].RenderTransform = poses == null ? Transform.Identity : new MatrixTransform(poses[i].Value);
        }
    }
}
