using Suzuryg.FaceEmo.Domain;
using Suzuryg.FaceEmo.Detail.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using Suzuryg.FaceEmo.Detail.Localization;
using UniRx;


namespace Suzuryg.FaceEmo.Detail.View.Element
{
    public class ParameterListElement : IDisposable
    {
        private static readonly int ScrollRightMargin = 5;
        private static readonly int Padding = 2;
        private static readonly int Margin = 2;
        private static readonly int ParameterNameWidth = 250;
        private static readonly int ParameterTypeWidth = 70;
        private static readonly int ValueWidth = 75;
        private static readonly int MinHeight = 100;

        public IObservable<(int branchIndex, Parameter parameter)> OnAddParameterButtonClicked => this._onAddParameterButtonClicked.AsObservable();
        public IObservable<(int branchIndex, int parameterIndex, Parameter parameter)> OnModifyParameterButtonClicked => this._onModifyParameterButtonClicked.AsObservable();
        public IObservable<(int branchIndex, int from, int to)> OnParameterOrderChanged => this._onParameterOrderChanged.AsObservable();
        public IObservable<(int branchIndex, int parameterIndex)> OnRemoveParameterButtonClicked => this._onRemoveParameterButtonClicked.AsObservable();

        private Subject<(int branchIndex, Parameter parameter)> _onAddParameterButtonClicked = new Subject<(int branchIndex, Parameter parameter)>();
        private Subject<(int branchIndex, int parameterIndex, Parameter parameter)> _onModifyParameterButtonClicked = new Subject<(int branchIndex, int parameterIndex, Parameter parameter)>();
        private Subject<(int branchIndex, int from, int to)> _onParameterOrderChanged = new Subject<(int branchIndex, int from, int to)>();
        private Subject<(int branchIndex, int parameterIndex)> _onRemoveParameterButtonClicked = new Subject<(int branchIndex, int parameterIndex)>();

        private int _branchIndex;
        private IReadOnlyList<Parameter> _parameters;

        private ReorderableList _reorderableList;
        private Vector2 _scrollPosition = Vector2.zero;

        private string _emptyText;
        private string _parameterText;

        private GUIStyle _centerStyle;
        
        private CompositeDisposable _disposables = new CompositeDisposable();

        public ParameterListElement(
            int branchIndex,
            IReadOnlyList<Parameter> parameters,
            IReadOnlyLocalizationSetting localizationSetting) 
        {
            // Dependencies
            _branchIndex = branchIndex;
            this._parameters = parameters;

            // Reorderable List
            _reorderableList = new ReorderableList(new List<Condition>(), typeof(Condition));
            _reorderableList.list = this._parameters.ToList();
            _reorderableList.headerHeight = EditorGUIUtility.singleLineHeight;
            _reorderableList.drawHeaderCallback = DrawHeader;
            _reorderableList.drawElementCallback = DrawElement;
            _reorderableList.drawNoneElementCallback = DrawEmpty;
            _reorderableList.onAddCallback = OnElementAdded;
            _reorderableList.onRemoveCallback = OnElementRemoved;
            _reorderableList.elementHeightCallback = GetElementHeight;
            _reorderableList.onReorderCallbackWithDetails = OnElementOrderChanged;

            // Styles
            try
            {
                _centerStyle = new GUIStyle(EditorStyles.label);
            }
            catch (NullReferenceException)
            {
                // Workaround for play mode
                _centerStyle = new GUIStyle();
            }
            _centerStyle.alignment = TextAnchor.MiddleCenter;

            // Set text
            SetText(localizationSetting.Table);
            localizationSetting.OnTableChanged.Synchronize().Subscribe(SetText).AddTo(_disposables);

        }

        public void Dispose()
        {
            _disposables.Dispose();
        }

        private void SetText(LocalizationTable localizationTable)
        {
            _emptyText = localizationTable.BranchListView_EmptyCondition;
            _parameterText = localizationTable.BranchListView_Parameter;
        }

        public void OnGUI(Rect rect)
        {
            var viewRect = new Rect(rect.x, rect.y,
                rect.width - EditorGUIUtility.singleLineHeight,
                _reorderableList.GetHeight());

            using (var scope = new GUI.ScrollViewScope(rect, _scrollPosition, viewRect))
            {
                _reorderableList?.DoList(rect);
                _scrollPosition = scope.scrollPosition;
            }
        }

        private float GetElementHeight(int index)
        {
            return EditorGUIUtility.singleLineHeight + Padding * 2;
        }

        private void DrawHeader(Rect rect)
        {
            GUI.Label(rect, this._parameterText);
        }

        public static float GetWidth()
        {
            return ReorderableList.Defaults.dragHandleWidth + ReorderableList.Defaults.padding + ScrollRightMargin
                + Padding + ParameterNameWidth + Margin + ParameterTypeWidth + Margin + ValueWidth + Padding;
        }

        public static float GetMinHeight() => MinHeight;

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (this._parameters.Count <= index)
            {
                return;
            }

            var parameter = this._parameters[index];

            var xBegin = rect.x + Padding;
            var yBegin = rect.y + Padding;
            var xCurrent = xBegin;
            var yCurrent = yBegin;
            
            // ParameterName
            var oldParameterName = parameter.ParameterName;
            var newParameterName = EditorGUI.TextField(new Rect(xCurrent, yCurrent, ParameterNameWidth, EditorGUIUtility.singleLineHeight),
                string.Empty, oldParameterName);
            if (oldParameterName != newParameterName) {
                var newParameter = new Parameter(newParameterName, parameter.ParameterType, parameter.Value);
                this._onModifyParameterButtonClicked.OnNext((this._branchIndex, index, newParameter));
            }
            xCurrent += ParameterNameWidth + Margin;
            
            // ParameterType
            var oldParameterType = (int)parameter.ParameterType;
            var newParameterType = EditorGUI.Popup(new Rect(xCurrent, yCurrent, ParameterTypeWidth, EditorGUIUtility.singleLineHeight),
                string.Empty, oldParameterType, Enum.GetNames(typeof(ParameterType)));
            if (newParameterType != oldParameterType) {
                var newParameter = new Parameter(parameter.ParameterName, (ParameterType)Enum.ToObject(typeof(ParameterType), newParameterType), parameter.Value);
                this._onModifyParameterButtonClicked.OnNext((this._branchIndex, index, newParameter));
            }
            xCurrent += ParameterTypeWidth + Margin;
            
            // Value
            var oldValue = parameter.Value;
            var newValue = oldValue;
            switch (parameter.ParameterType) {
                case ParameterType.Bool:
                    newValue = EditorGUI.Toggle(new Rect(xCurrent, yCurrent, ValueWidth, EditorGUIUtility.singleLineHeight),
                        string.Empty, oldValue > 0.1f) ? 1 : 0;
                    break;
                case ParameterType.Int:
                    newValue = EditorGUI.IntField(new Rect(xCurrent, yCurrent, ValueWidth, EditorGUIUtility.singleLineHeight),
                        string.Empty, (int)oldValue);
                    break;
                case ParameterType.Float:
                    newValue = EditorGUI.FloatField(new Rect(xCurrent, yCurrent, ValueWidth, EditorGUIUtility.singleLineHeight),
                        string.Empty, oldValue);
                    break;
                default:
                    throw new FaceEmoException("Unknown parameter type.");;
            }
            if (!Mathf.Approximately(oldValue, newValue)) {
                var newParameter = new Parameter(parameter.ParameterName, parameter.ParameterType, newValue);
                this._onModifyParameterButtonClicked.OnNext((this._branchIndex, index, newParameter));
            }
        }

        private void DrawEmpty(Rect rect)
        {
            GUI.Label(rect, _emptyText, _centerStyle);
        }

        private void OnElementAdded(ReorderableList reorderableList)
        {
            this._onAddParameterButtonClicked.OnNext((_branchIndex, new Parameter(string.Empty, ParameterType.Int, 0)));
        }

        private void OnElementRemoved(ReorderableList reorderableList)
        {
            this._onRemoveParameterButtonClicked.OnNext((_branchIndex, _reorderableList.index));
        }

        private void OnElementOrderChanged(ReorderableList reorderableList, int oldIndex, int newIndex)
        {
            this._onParameterOrderChanged.OnNext((_branchIndex, oldIndex, newIndex));
        }
    }
}