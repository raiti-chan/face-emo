using Suzuryg.FaceEmo.Domain;
using Suzuryg.FaceEmo.UseCase;
using Suzuryg.FaceEmo.Components.Settings;
using Suzuryg.FaceEmo.Components.States;
using Suzuryg.FaceEmo.Detail.AV3;
using Suzuryg.FaceEmo.Detail.Drawing;
using Suzuryg.FaceEmo.Detail.Localization;
using Suzuryg.FaceEmo.Detail.View.Element;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UniRx;
using Suzuryg.FaceEmo.UseCase.ModifyMenu.ModifyMode;
using Suzuryg.FaceEmo.UseCase.ModifyMenu.ModifyMode.ModifyAnimation;
using System.Linq;
using System.Text.RegularExpressions;

namespace Suzuryg.FaceEmo.Detail.View
{
    public class GestureTableView : IDisposable
    {
        private IAddBranchUseCase _addBranchUseCase;
        private ISetExistingAnimationUseCase _setExistingAnimationUseCase;
        private IModifyBranchPropertiesUseCase _modifyBranchPropertiesUseCase;

        private IReadOnlyLocalizationSetting _localizationSetting;
        private ISubWindowProvider _subWindowProvider;
        private DefaultsProviderGenerator _defaultProviderGenerator;
        private UpdateMenuSubject _updateMenuSubject;
        private SelectionSynchronizer _selectionSynchronizer;
        private GestureTableThumbnailDrawer _thumbnailDrawer;
        private SerializedObject _thumbnailSetting;

        private GestureTableElement _gestureTableElement;
        private AnimationElement _animationElement;
        private AV3.ExpressionEditor _expressionEditor;

        private IMGUIContainer _gestureTableContainer;
        private Label _thumbnailWidthLabel;
        private Label _thumbnailHeightLabel;
        private SliderInt _thumbnailWidthSlider;
        private SliderInt _thumbnailHeightSlider;
        private Toggle _showClipFieldToggle;
        private Button _generateAllGestureCombinationAnimationButton;

        private StyleColor _canAddButtonColor = Color.black;
        private StyleColor _canAddButtonBackgroundColor = Color.yellow;
        private StyleColor _canNotAddButtonColor;
        private StyleColor _canNotAddButtonBackgroundColor;

        private CompositeDisposable _disposables = new CompositeDisposable();

        public GestureTableView(
            IAddBranchUseCase addBranchUseCase,
            ISetExistingAnimationUseCase setExistingAnimationUseCase,
            IModifyBranchPropertiesUseCase modifyBranchPropertiesUseCase,
            IReadOnlyLocalizationSetting localizationSetting,
            ISubWindowProvider subWindowProvider,
            DefaultsProviderGenerator defaultProviderGenerator,
            UpdateMenuSubject updateMenuSubject,
            SelectionSynchronizer selectionSynchronizer,
            GestureTableThumbnailDrawer thumbnailDrawer,
            GestureTableElement gestureTableElement,
            AnimationElement animationElement,
            AV3.ExpressionEditor expressionEditor,
            ThumbnailSetting thumbnailSetting)
        {
            // Usecases
            _addBranchUseCase = addBranchUseCase;
            _setExistingAnimationUseCase = setExistingAnimationUseCase;
            _modifyBranchPropertiesUseCase = modifyBranchPropertiesUseCase;

            // Others
            _localizationSetting = localizationSetting;
            _subWindowProvider = subWindowProvider;
            _defaultProviderGenerator = defaultProviderGenerator;
            _updateMenuSubject = updateMenuSubject;
            _selectionSynchronizer = selectionSynchronizer;
            _thumbnailDrawer = thumbnailDrawer;
            _gestureTableElement = gestureTableElement;
            _animationElement = animationElement;
            _expressionEditor = expressionEditor;
            _thumbnailSetting = new SerializedObject(thumbnailSetting);

            // Gesture table element
            _gestureTableElement.AddTo(_disposables);
            _gestureTableElement.OnSelectionChanged.Synchronize().Subscribe(OnSelectionChanged).AddTo(_disposables);
            _gestureTableElement.OnBranchIndexExceeded.Synchronize().Subscribe(_ => OnBranchIndexExceeded()).AddTo(_disposables);
            _gestureTableElement.OnAddBrandchButtonClicked.Synchronize().Subscribe(OnAddBranchButtonClicked).AddTo(_disposables);
            _gestureTableElement.OnEditClipButtonClicked.Synchronize().Subscribe(OnEditClipButtonClicked).AddTo(_disposables);
            _gestureTableElement.OnCombineButtonClicked.Synchronize().Subscribe(OnCombineButtonClicked).AddTo(_disposables);
            _gestureTableElement.OnBaseAnimationChanged.Synchronize().Subscribe(OnBaseAnimationChanged).AddTo(_disposables);

            // Localization table changed event handler
            _localizationSetting.OnTableChanged.Synchronize().Subscribe(SetText).AddTo(_disposables);

            // Update menu event handler
            _updateMenuSubject.Observable.Synchronize().Subscribe(x => OnMenuUpdated(x.menu)).AddTo(_disposables);

            // Synchronize selection event handler
            _selectionSynchronizer.OnSynchronizeSelection.Synchronize().Subscribe(OnSynchronizeSelection).AddTo(_disposables);

            // Repaint thumbnail event handler
            _thumbnailDrawer.OnThumbnailUpdated.Synchronize().ObserveOnMainThread().Subscribe(_ => _gestureTableContainer?.MarkDirtyRepaint()).AddTo(_disposables);
        }

        public void Dispose()
        {
            _thumbnailWidthSlider.UnregisterValueChangedCallback(OnThumbnailSizeChanged);
            _thumbnailHeightSlider.UnregisterValueChangedCallback(OnThumbnailSizeChanged);
            _showClipFieldToggle.UnregisterValueChangedCallback(OnShowClipFieldValueChanged);
            _disposables.Dispose();
        }

        public void Initialize(VisualElement root)
        {
            // Load UXML and style
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{DetailConstants.ViewDirectory}/{nameof(GestureTableView)}.uxml");
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{DetailConstants.ViewDirectory}/{nameof(GestureTableView)}.uss");
            NullChecker.Check(uxml, style);

            root.styleSheets.Add(style);
            uxml.CloneTree(root);

            // Query Elements
            _gestureTableContainer = root.Q<IMGUIContainer>("GestureTableContainer");
            _thumbnailWidthLabel = root.Q<Label>("ThumbnailWidthLabel");
            _thumbnailHeightLabel = root.Q<Label>("ThumbnailHeightLabel");
            _thumbnailWidthSlider = root.Q<SliderInt>("ThumbnailWidthSlider");
            _thumbnailHeightSlider = root.Q<SliderInt>("ThumbnailHeightSlider");
            _showClipFieldToggle = root.Q<Toggle>("ShowClipFieldToggle");
            _generateAllGestureCombinationAnimationButton = root.Q<Button>("GenerateAllGestureCombinationAnimation");
            NullChecker.Check(_gestureTableContainer, _thumbnailWidthLabel, _thumbnailHeightLabel, _thumbnailWidthSlider, _thumbnailHeightSlider, _showClipFieldToggle, _generateAllGestureCombinationAnimationButton);

            // Add event handlers
            Observable.FromEvent(x => _gestureTableContainer.onGUIHandler += x, x => _gestureTableContainer.onGUIHandler -= x)
                .Synchronize().Subscribe(_ =>
                {
                    _gestureTableElement?.OnGUI(_gestureTableContainer.contentRect);

                    // To draw gesture cell selection
                    if (Event.current.type == EventType.MouseUp || Event.current.type == EventType.MouseDown)
                    {
                        _gestureTableContainer.MarkDirtyRepaint();
                    }
                }).AddTo(_disposables);

            // Initialize fields
            _thumbnailSetting.Update();

            _thumbnailWidthSlider.lowValue = ThumbnailSetting.GestureTable_MinWidth;
            _thumbnailWidthSlider.highValue = ThumbnailSetting.GestureTable_MaxWidth;
            _thumbnailWidthSlider.value = _thumbnailSetting.FindProperty(nameof(ThumbnailSetting.GestureTable_Width)).intValue;

            _thumbnailWidthSlider.bindingPath = nameof(ThumbnailSetting.GestureTable_Width);
            _thumbnailWidthSlider.BindProperty(_thumbnailSetting);

            _thumbnailHeightSlider.lowValue = ThumbnailSetting.GestureTable_MinHeight;
            _thumbnailHeightSlider.highValue = ThumbnailSetting.GestureTable_MaxHeight;
            _thumbnailHeightSlider.value = _thumbnailSetting.FindProperty(nameof(ThumbnailSetting.GestureTable_Height)).intValue;

            _thumbnailHeightSlider.bindingPath = nameof(ThumbnailSetting.GestureTable_Height);
            _thumbnailHeightSlider.BindProperty(_thumbnailSetting);

            _showClipFieldToggle.value = EditorPrefs.GetBool(DetailConstants.KeyShowClipFieldInGestureTable, DetailConstants.DefaultShowClipFieldInGestureTable);

            // Add event handlers
            // Delay event registration due to unstable slider values immediately after opening the window.
            Observable.Timer(TimeSpan.FromMilliseconds(100)).ObserveOnMainThread().Subscribe(_ =>
            {
                _thumbnailWidthSlider.RegisterValueChangedCallback(OnThumbnailSizeChanged);
                _thumbnailHeightSlider.RegisterValueChangedCallback(OnThumbnailSizeChanged);
            }).AddTo(_disposables);
            _showClipFieldToggle.RegisterValueChangedCallback(OnShowClipFieldValueChanged);
            _generateAllGestureCombinationAnimationButton.clicked += OnGenerateAllGestureCombinationAnimationButtonClicked;

            // Set text
            SetText(_localizationSetting.Table);
        }

        private void SetText(LocalizationTable localizationTable)
        {
            if (_thumbnailWidthLabel != null) { _thumbnailWidthLabel.text = localizationTable.Common_ThumbnailWidth; }
            if (_thumbnailHeightLabel != null) { _thumbnailHeightLabel.text = localizationTable.Common_ThumbnailHeight; }
            if (_showClipFieldToggle != null) { _showClipFieldToggle.text = localizationTable.GestureTableView_ShowClipField; }
        }

        private void OnMenuUpdated(IMenu menu)
        {
            _gestureTableElement.Setup(menu);
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            _gestureTableContainer?.MarkDirtyRepaint();
        }

        private void OnSelectionChanged((HandGesture left, HandGesture right)? args)
        {
            if (!(args is null))
            {
                _selectionSynchronizer.ChangeGestureTableViewSelection(args.Value.left, args.Value.right);
            }
        }

        private void OnBranchIndexExceeded()
        {
            var menu = _gestureTableElement.Menu;
            if (menu is null || !menu.ContainsMode(_gestureTableElement.SelectedModeId))
            {
                return;
            }

            var mode = menu.GetMode(_gestureTableElement.SelectedModeId);
            var lastBranchIndex = mode.Branches.Count - 1;
            _selectionSynchronizer.ChangeBranchListViewSelection(lastBranchIndex);
        }

        private void OnSynchronizeSelection(ViewSelection viewSelection)
        {
            _gestureTableElement?.ChangeSelection(viewSelection.MenuItemListView, viewSelection.BranchListView, viewSelection.GestureTableView);
            UpdateDisplay();
        }

        private void OnThumbnailSizeChanged(ChangeEvent<int> changeEvent)
        {
            // TODO: Reduce unnecessary redrawing
            _thumbnailDrawer.RequestUpdateAll();
        }

        private void OnShowClipFieldValueChanged(ChangeEvent<bool> changeEvent)
        {
            EditorPrefs.SetBool(DetailConstants.KeyShowClipFieldInGestureTable, changeEvent.newValue);
        }

        private void OnAddBranchButtonClicked((HandGesture left, HandGesture right)? args)
        {
            if (!args.HasValue) { return; }

            var conditions = new[]
            {
                new Condition(Hand.Left, args.Value.left, ComparisonOperator.Equals),
                new Condition(Hand.Right, args.Value.right, ComparisonOperator.Equals),
            };
            _addBranchUseCase.Handle("", _gestureTableElement.SelectedModeId,
                conditions: conditions,
                order: 0,
                defaultsProvider: _defaultProviderGenerator.Generate());
        }

        private void OnEditClipButtonClicked((HandGesture left, HandGesture right)? args)
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog(DomainConstants.SystemName, _localizationSetting.GetCurrentLocaleTable().Common_Message_NotPossibleInPlayMode, "OK"); return; }
            else if (!args.HasValue) { return; }
            else if (_gestureTableElement?.Menu?.ContainsMode(_gestureTableElement?.SelectedModeId) != true) { return; }

            var mode = _gestureTableElement.Menu.GetMode(_gestureTableElement.SelectedModeId);
            var selectedBranch = mode.GetGestureCell(args.Value.left, args.Value.right);
            if (selectedBranch == null) { return; }

            for (int branchIndex = 0; branchIndex < mode.Branches.Count; branchIndex++)
            {
                if (ReferenceEquals(selectedBranch, mode.Branches[branchIndex]))
                {
                    CreateAndOpenClip(branchIndex);
                    break;
                }
            }
        }

        private void CreateAndOpenClip(int branchIndex)
        {
            var modeId = _gestureTableElement.SelectedModeId;
            var mode = _gestureTableElement.Menu.GetMode(modeId);
            var animation = mode.Branches[branchIndex].BaseAnimation;
            var path = AssetDatabase.GUIDToAssetPath(animation?.GUID);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            var clipExists = clip != null;
            if (clipExists)
            {
                _expressionEditor.Open(clip);
            }
            else
            {
                var guid = _animationElement.GetAnimationGuidWithDialog(AnimationElement.DialogMode.Create, path, defaultClipName: null);
                if (!string.IsNullOrEmpty(guid))
                {
                    _expressionEditor.Open(AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid)));
                    _setExistingAnimationUseCase.Handle(string.Empty, new Domain.Animation(guid), modeId, branchIndex, BranchAnimationType.Base);
                }
            }
        }
        private void OnGenerateAllGestureCombinationAnimationButtonClicked() {
            if (!EditorUtility.DisplayDialog(DomainConstants.SystemName, "組み合わせ表情の自動生成を行います。\r\nよろしいですか？", "実行", "キャンセル")) return;
            var modeId = _gestureTableElement.SelectedModeId;
            var mode = _gestureTableElement.Menu.GetMode(modeId);


            var saveDir = EditorUtility.SaveFolderPanel(title: null, defaultName: null, folder: "Assets/");
            if (string.IsNullOrEmpty(saveDir)) return;
            saveDir = PathConverter.ToUnityPath(saveDir);

            var handGestures = Enum.GetValues(typeof(HandGesture))
                .Cast<HandGesture>()
                .Where(gesture => gesture != HandGesture.Neutral)
                .ToArray();

            foreach ((var left, var right) in handGestures.SelectMany(left => handGestures.Select(right => (left, right))))
            {
                var targetBranch = mode.GetGestureCell(left, right);
                var conditions = new[]
                {
                    new Condition(Hand.Left, left, ComparisonOperator.Equals),
                    new Condition(Hand.Right, right, ComparisonOperator.Equals)
                };
                
                int branchIndex;
                
                var leftCell = mode.GetGestureCell(left, HandGesture.Neutral);
                var rightCell = mode.GetGestureCell(HandGesture.Neutral, right);
                
                if (targetBranch != null && targetBranch.Conditions.Count == 2 && targetBranch.Conditions.Contains(conditions[0]) && targetBranch.Conditions.Contains(conditions[1])) {
                    // 既に存在するブランチ
                    var found = false;
                    for (branchIndex = 0; branchIndex < mode.Branches.Count; branchIndex++) 
                    {
                        if (ReferenceEquals(targetBranch, mode.Branches[branchIndex])) {
                            found = true;
                            break;
                        }
                    }
                    if (!found) { throw new FaceEmoException("The target branch was not found."); }

                    var baseAnimationGuid = targetBranch.BaseAnimation.GUID;
                    var baseAnimationPath = AssetDatabase.GUIDToAssetPath(baseAnimationGuid);
                    if (!string.IsNullOrEmpty(baseAnimationPath)) {
                        var nameRegex = new Regex(@".+autogen-.+\.anim");
                        if (!nameRegex.Match(baseAnimationPath).Success) { 
                            continue;
                        }
                    }

                } else {
                    // 新規ブランチ
                    DefaultsProvider defaultsProvider = _defaultProviderGenerator.Generate();
                    defaultsProvider.BlinkEnabled = leftCell?.BlinkEnabled == false || rightCell?.BlinkEnabled == false ? false : defaultsProvider.BlinkEnabled;
                    defaultsProvider.EyeTrackingControl = leftCell?.EyeTrackingControl == EyeTrackingControl.Animation || rightCell?.EyeTrackingControl == EyeTrackingControl.Animation ? EyeTrackingControl.Animation : defaultsProvider.EyeTrackingControl;
                    defaultsProvider.MouthTrackingControl = leftCell?.MouthTrackingControl == MouthTrackingControl.Animation || rightCell?.MouthTrackingControl == MouthTrackingControl.Animation ? MouthTrackingControl.Animation : defaultsProvider.MouthTrackingControl;
                    defaultsProvider.MouthMorphCancelerEnabled = leftCell?.MouthMorphCancelerEnabled == true || rightCell?.MouthMorphCancelerEnabled == true ? true : defaultsProvider.MouthMorphCancelerEnabled;
                    _addBranchUseCase.Handle("", _gestureTableElement.SelectedModeId, 
                        conditions: conditions,
                        order: 0,
                        defaultsProvider: defaultsProvider);
                    branchIndex = 0;
                    _selectionSynchronizer.ChangeGestureTableViewSelection(left, right);
                    
                }

                
                {
                    // Base アニメーション
                    var newClip = new AnimationClip();
                    var newClipPath = saveDir + "/autogen-" + left + '-' + right + ".anim";
                    AssetDatabase.CreateAsset(newClip, newClipPath);
                    var newClipGuid = AssetDatabase.AssetPathToGUID(newClipPath);
                    if (string.IsNullOrEmpty(newClipGuid)) {
                        AssetDatabase.Refresh();
                        newClipGuid = AssetDatabase.AssetPathToGUID(newClipPath);
                    }
                    var leftClip = AV3Utility.GetAnimationClipWithName(leftCell?.BaseAnimation).clip;
                    var rightClip = AV3Utility.GetAnimationClipWithName(rightCell?.BaseAnimation).clip;
                    AV3Utility.CombineExpressions(leftClip, rightClip, newClip);
                    _setExistingAnimationUseCase.Handle(string.Empty, new Domain.Animation(newClipGuid), modeId, branchIndex, BranchAnimationType.Base);
                }
                bool useLeftTrigger = false;
                bool useRightTrigger = false;
                
                if (leftCell?.LeftHandAnimation != null) {
                    // Left Trigger アニメーション
                    var newClip = new AnimationClip();
                    var newClipPath = saveDir + "/autogen-" + left + '-' + right + "_HandL.anim";
                    AssetDatabase.CreateAsset(newClip, newClipPath);
                    var newClipGuid = AssetDatabase.AssetPathToGUID(newClipPath);
                    if (string.IsNullOrEmpty(newClipGuid)) {
                        AssetDatabase.Refresh();
                        newClipGuid = AssetDatabase.AssetPathToGUID(newClipPath);
                    }
                    var leftClip = AV3Utility.GetAnimationClipWithName(leftCell?.LeftHandAnimation).clip;
                    var rightClip = AV3Utility.GetAnimationClipWithName(rightCell?.BaseAnimation).clip;
                    AV3Utility.CombineExpressions(leftClip, rightClip, newClip);
                    _setExistingAnimationUseCase.Handle(string.Empty, new Domain.Animation(newClipGuid), modeId, branchIndex, BranchAnimationType.Left);
                    useLeftTrigger = true;
                }

                if (rightCell?.RightHandAnimation != null) {
                    // Right Trigger アニメーション
                    var newClip = new AnimationClip();
                    var newClipPath = saveDir + "/autogen-" + left + '-' + right + "_HandR.anim";
                    AssetDatabase.CreateAsset(newClip, newClipPath);
                    var newClipGuid = AssetDatabase.AssetPathToGUID(newClipPath);
                    if (string.IsNullOrEmpty(newClipGuid)) {
                        AssetDatabase.Refresh();
                        newClipGuid = AssetDatabase.AssetPathToGUID(newClipPath);
                    }
                    var leftClip = AV3Utility.GetAnimationClipWithName(leftCell?.BaseAnimation).clip;
                    var rightClip = AV3Utility.GetAnimationClipWithName(rightCell?.RightHandAnimation).clip;
                    AV3Utility.CombineExpressions(leftClip, rightClip, newClip);
                    _setExistingAnimationUseCase.Handle(string.Empty, new Domain.Animation(newClipGuid), modeId, branchIndex, BranchAnimationType.Right);
                    useRightTrigger = true;
                }

                if (leftCell?.LeftHandAnimation != null && rightCell?.RightHandAnimation != null) {
                    // Both Trigger アニメーション
                    var newClip = new AnimationClip();
                    var newClipPath = saveDir + "/autogen-" + left + '-' + right + "_HandBoth.anim";
                    AssetDatabase.CreateAsset(newClip, newClipPath);
                    var newClipGuid = AssetDatabase.AssetPathToGUID(newClipPath);
                    if (string.IsNullOrEmpty(newClipGuid)) {
                        AssetDatabase.Refresh();
                        newClipGuid = AssetDatabase.AssetPathToGUID(newClipPath);
                    }

                    var leftClip = AV3Utility.GetAnimationClipWithName(leftCell?.LeftHandAnimation).clip;
                    var rightClip = AV3Utility.GetAnimationClipWithName(rightCell?.RightHandAnimation).clip;
                    AV3Utility.CombineExpressions(leftClip, rightClip, newClip);
                    _setExistingAnimationUseCase.Handle(string.Empty, new Domain.Animation(newClipGuid), modeId, branchIndex, BranchAnimationType.Both);
                }
                
                _modifyBranchPropertiesUseCase.Handle(string.Empty, modeId, branchIndex, isLeftTriggerUsed:useLeftTrigger, isRightTriggerUsed:useRightTrigger);
            }
        }

        private void OnCombineButtonClicked((HandGesture left, HandGesture right)? args)
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog(DomainConstants.SystemName, _localizationSetting.GetCurrentLocaleTable().Common_Message_NotPossibleInPlayMode, "OK"); return; }
            else if (!args.HasValue) { return; }
            else if (_gestureTableElement?.Menu?.ContainsMode(_gestureTableElement?.SelectedModeId) != true) { return; }

            var modeId = _gestureTableElement.SelectedModeId;
            var mode = _gestureTableElement.Menu.GetMode(modeId);
            var targetBranch = mode.GetGestureCell(args.Value.left, args.Value.right);
            var conditions = new[]
            {
                new Condition(Hand.Left, args.Value.left, ComparisonOperator.Equals),
                new Condition(Hand.Right, args.Value.right, ComparisonOperator.Equals),
            };
            int branchIndex;

            // Create new clip
            var newClipGuid = _animationElement.GetAnimationGuidWithDialog(AnimationElement.DialogMode.Create, string.Empty, defaultClipName: null);
            if (string.IsNullOrEmpty(newClipGuid)) { return; }

            // Use existing branch
            if (targetBranch != null &&
                targetBranch.Conditions.Count == 2 &&
                targetBranch.Conditions.Contains(conditions[0]) &&
                targetBranch.Conditions.Contains(conditions[1]))
            {
                var found = false;
                for (branchIndex = 0; branchIndex < mode.Branches.Count; branchIndex++)
                {
                    if (ReferenceEquals(targetBranch, mode.Branches[branchIndex]))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) { throw new FaceEmoException("The target branch was not found."); }
            }
            // Add new branch
            else
            {
                _addBranchUseCase.Handle("", _gestureTableElement.SelectedModeId,
                    conditions: conditions,
                    order: 0,
                    defaultsProvider: _defaultProviderGenerator.Generate());

                branchIndex = 0;

                _selectionSynchronizer.ChangeGestureTableViewSelection(args.Value.left, args.Value.right);
            }

            // Combine clips
            var newClipPath = AssetDatabase.GUIDToAssetPath(newClipGuid);
            var newClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(newClipPath);

            var leftClip = AV3Utility.GetAnimationClipWithName(mode.GetGestureCell(args.Value.left, HandGesture.Neutral)?.BaseAnimation).clip;
            var rightClip = AV3Utility.GetAnimationClipWithName(mode.GetGestureCell(HandGesture.Neutral, args.Value.right)?.BaseAnimation).clip;

            AV3Utility.CombineExpressions(leftClip, rightClip, newClip);

            _expressionEditor.Open(AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(newClipGuid)));
            _setExistingAnimationUseCase.Handle(string.Empty, new Domain.Animation(newClipGuid), modeId, branchIndex, BranchAnimationType.Base);
        }

        private void OnBaseAnimationChanged((string clipGUID, HandGesture left, HandGesture right)? args)
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog(DomainConstants.SystemName, _localizationSetting.GetCurrentLocaleTable().Common_Message_NotPossibleInPlayMode, "OK"); return; }
            else if (!args.HasValue) { return; }
            else if (_gestureTableElement?.Menu?.ContainsMode(_gestureTableElement?.SelectedModeId) != true) { return; }

            var modeId = _gestureTableElement.SelectedModeId;
            var mode = _gestureTableElement.Menu.GetMode(modeId);
            var targetBranch = mode.GetGestureCell(args.Value.left, args.Value.right);
            if (targetBranch == null) { return; }

            for (int branchIndex = 0; branchIndex < mode.Branches.Count; branchIndex++)
            {
                if (ReferenceEquals(targetBranch, mode.Branches[branchIndex]))
                {
                    _setExistingAnimationUseCase.Handle(string.Empty, new Domain.Animation(args.Value.clipGUID), modeId, branchIndex, BranchAnimationType.Base);

                    // Select target branch
                    _selectionSynchronizer.ChangeGestureTableViewSelection(args.Value.left, args.Value.right);

                    break;
                }
            }
        }
    }
}
