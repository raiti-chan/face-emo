﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Suzuryg.FacialExpressionSwitcher.Domain
{
    public interface IBranch
    {
        EyeTrackingControl EyeTrackingControl { get; }
        MouthTrackingControl MouthTrackingControl { get; }
        bool IsLeftTriggerUsed { get; }
        bool IsRightTriggerUsed { get; }

        IReadOnlyList<Condition> Conditions { get; }
        bool IsReachable { get; }
        bool CanLeftTriggerUsed { get; }
        bool CanRightTriggerUsed { get; }

        IAnimation BaseAnimation { get; }
        IAnimation LeftHandAnimation { get; }
        IAnimation RightHandAnimation { get; }
        IAnimation BothHandsAnimation { get; }
    }

    public class Branch : IBranch
    {
        public EyeTrackingControl EyeTrackingControl { get; set; } = EyeTrackingControl.Tracking;
        public MouthTrackingControl MouthTrackingControl { get; set; } = MouthTrackingControl.Tracking;
        public bool IsLeftTriggerUsed { get; set; } = false;
        public bool IsRightTriggerUsed { get; set; } = false;

        public IReadOnlyList<Condition> Conditions => _conditions;
        public bool IsReachable { get; set; } = false;
        public bool CanLeftTriggerUsed { get; set; } = false;
        public bool CanRightTriggerUsed { get; set; } = false;

        public IAnimation BaseAnimation { get; private set; }
        public IAnimation LeftHandAnimation { get; private set; }
        public IAnimation RightHandAnimation { get; private set; }
        public IAnimation BothHandsAnimation { get; private set; }

        private List<Condition> _conditions = new List<Condition>();

        public Branch(IEnumerable<Condition> conditions = null)
        {
            if (conditions is IEnumerable<Condition>)
            {
                _conditions = conditions.ToList();
            }
        }

        public void AddCondition(Condition condition)
        {
            _conditions.Add(condition);
        }

        public void ModifyCondition(int index, Condition condition)
        {
            _conditions[index] = condition;
        }

        public void RemoveCondition(int index)
        {
            _conditions.RemoveAt(index);
        }

        public void ChangeBranchOrder(int from, int to)
        {
            var condition = _conditions[from];
            _conditions.RemoveAt(from);

            if (to < 0)
            {
                _conditions.Insert(0, condition);
            }
            else if (to > _conditions.Count)
            {
                _conditions.Add(condition);
            }
            else
            {
                _conditions.Insert(to, condition);
            }
        }

        public bool IsMatched(HandGesture left, HandGesture right)
        {
            if (!Conditions.Any())
            {
                return false;
            }

            foreach (var condition in Conditions)
            {
                switch (condition.Hand)
                {
                    case Hand.Left:
                        switch (condition.ComparisonOperator)
                        {
                            case ComparisonOperator.Equals:
                                if (left != condition.HandGesture) { return false; }
                                break;
                            case ComparisonOperator.NotEqual:
                                if (left == condition.HandGesture) { return false; }
                                break;
                            default:
                                throw new FacialExpressionSwitcherException("Unknown comparison operator.");
                        }
                        break;
                    case Hand.Right:
                        switch (condition.ComparisonOperator)
                        {
                            case ComparisonOperator.Equals:
                                if (right != condition.HandGesture) { return false; }
                                break;
                            case ComparisonOperator.NotEqual:
                                if (right == condition.HandGesture) { return false; }
                                break;
                            default:
                                throw new FacialExpressionSwitcherException("Unknown comparison operator.");
                        }
                        break;
                    case Hand.OneSide:
                        switch (condition.ComparisonOperator)
                        {
                            case ComparisonOperator.Equals:
                                if (left != condition.HandGesture && right != condition.HandGesture) { return false; }
                                else if (left == condition.HandGesture && right == condition.HandGesture) { return false; }
                                break;
                            case ComparisonOperator.NotEqual:
                                if (left != condition.HandGesture && right != condition.HandGesture) { return false; }
                                else if (left == condition.HandGesture && right == condition.HandGesture) { return false; }
                                break;
                            default:
                                throw new FacialExpressionSwitcherException("Unknown comparison operator.");
                        }
                        break;
                    case Hand.Either:
                        switch (condition.ComparisonOperator)
                        {
                            case ComparisonOperator.Equals:
                                if (left != condition.HandGesture && right != condition.HandGesture) { return false; }
                                break;
                            case ComparisonOperator.NotEqual:
                                if (left == condition.HandGesture && right == condition.HandGesture) { return false; }
                                break;
                            default:
                                throw new FacialExpressionSwitcherException("Unknown comparison operator.");
                        }
                        break;
                    case Hand.Both:
                        switch (condition.ComparisonOperator)
                        {
                            case ComparisonOperator.Equals:
                                if (left != condition.HandGesture || right != condition.HandGesture) { return false; }
                                break;
                            case ComparisonOperator.NotEqual:
                                if (left == condition.HandGesture || right == condition.HandGesture) { return false; }
                                break;
                            default:
                                throw new FacialExpressionSwitcherException("Unknown comparison operator.");
                        }
                        break;
                    default:
                        throw new FacialExpressionSwitcherException("Unknown hand type.");
                }
            }

            return true;
        }

        public void SetAnimation(IAnimation animation, BranchAnimationType? branchAnimationType)
        {
            switch (branchAnimationType)
            {
                case BranchAnimationType.Base:
                    BaseAnimation = animation;
                    break;
                case BranchAnimationType.Left:
                    LeftHandAnimation = animation;
                    break;
                case BranchAnimationType.Right:
                    RightHandAnimation = animation;
                    break;
                case BranchAnimationType.Both:
                    BothHandsAnimation = animation;
                    break;
                default:
                    throw new FacialExpressionSwitcherException("Invalid BranchAnimationType.");
            }
        }

        /*
        private void UpdateTriggerStatus()
        {
            CanLeftTriggerUsed = false;
            CanRightTriggerUsed = false;

            foreach (var right in Mode.GestureList)
            {
                if (IsMatched(HandGesture.Fist, right))
                {
                    CanLeftTriggerUsed = true;
                    break;
                }
            }

            foreach (var left in Mode.GestureList)
            {
                if (IsMatched(left, HandGesture.Fist))
                {
                    CanRightTriggerUsed = true;
                    break;
                }
            }
        }
        */
    }
}
