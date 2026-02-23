using Suzuryg.FaceEmo.Domain;
using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace Suzuryg.FaceEmo.UseCase.ModifyMenu.ModifyMode.ModifyBranch
{
    public interface IRemoveParameterUseCase
    {
        void Handle(string menuId, string modeId, int branchIndex, int parameterIndex);
    }

    public interface IRemoveParameterPresenter
    {
        IObservable<(RemoveParameterResult removeParameterResult, IMenu menu, string errorMessage)> Observable { get; }

        void Complete(RemoveParameterResult removeParameterResult, in IMenu menu, string errorMessage = "");
    }

    public enum RemoveParameterResult
    {
        Succeeded,
        MenuDoesNotExist,
        InvalidParameter,
        ArgumentNull,
        Error,
    }

    public class RemoveParameterPresenter : IRemoveParameterPresenter
    {
        public IObservable<(RemoveParameterResult, IMenu, string)> Observable => _subject.AsObservable().Synchronize();

        private Subject<(RemoveParameterResult, IMenu, string)> _subject = new Subject<(RemoveParameterResult, IMenu, string)>();

        public void Complete(RemoveParameterResult removeParameterResult, in IMenu menu, string errorMessage = "")
        {
            _subject.OnNext((removeParameterResult, menu, errorMessage));
        }
    }

    public class RemoveParameterUseCase : IRemoveParameterUseCase
    {
        IMenuRepository _menuRepository;
        UpdateMenuSubject _updateMenuSubject;
        IRemoveParameterPresenter _removeParameterPresenter;

        public RemoveParameterUseCase(IMenuRepository menuRepository, UpdateMenuSubject updateMenuSubject, IRemoveParameterPresenter removeParameterPresenter)
        {
            _menuRepository = menuRepository;
            _updateMenuSubject = updateMenuSubject;
            _removeParameterPresenter = removeParameterPresenter;
        }

        public void Handle(string menuId, string modeId, int branchIndex, int parameterIndex)
        {
            try
            {
                if (menuId is null || modeId is null)
                {
                    _removeParameterPresenter.Complete(RemoveParameterResult.ArgumentNull, null);
                    return;
                }

                if (!_menuRepository.Exists(menuId))
                {
                    _removeParameterPresenter.Complete(RemoveParameterResult.MenuDoesNotExist, null);
                    return;
                }

                var menu = _menuRepository.Load(menuId);

                if (!menu.CanRemoveParameter(modeId, branchIndex, parameterIndex))
                {
                    _removeParameterPresenter.Complete(RemoveParameterResult.InvalidParameter, menu);
                    return;
                }

                menu.RemoveParameter(modeId, branchIndex, parameterIndex);

                _menuRepository.Save(menuId, menu, "RemoveParameter");
                _removeParameterPresenter.Complete(RemoveParameterResult.Succeeded, menu);
                _updateMenuSubject.OnNext(menu);
            }
            catch (Exception ex)
            {
                _removeParameterPresenter.Complete(RemoveParameterResult.Error, null, ex.ToString());
            }
        }
    }
}
