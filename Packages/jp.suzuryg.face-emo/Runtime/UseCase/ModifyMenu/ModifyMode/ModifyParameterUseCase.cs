using Suzuryg.FaceEmo.Domain;
using System;
using System.Collections.Generic;
using UniRx;

namespace Suzuryg.FaceEmo.UseCase.ModifyMenu.ModifyMode.ModifyBranch
{
    public interface IModifyParameterUseCase
    {
        void Handle(string menuId, string modeId, int branchIndex, int conditionIndex, Parameter condition);
    }

    public interface IModifyParameterPresenter
    {
        IObservable<(ModifyParameterResult modifyParameterResult, IMenu menu, string errorMessage)> Observable { get; }

        void Complete(ModifyParameterResult modifyParameterResult, in IMenu menu, string errorMessage = "");
    }

    public enum ModifyParameterResult
    {
        Succeeded,
        MenuDoesNotExist,
        InvalidParameter,
        ArgumentNull,
        Error,
    }

    public class ModifyParameterPresenter : IModifyParameterPresenter
    {
        public IObservable<(ModifyParameterResult, IMenu, string)> Observable => _subject.AsObservable().Synchronize();

        private Subject<(ModifyParameterResult, IMenu, string)> _subject = new Subject<(ModifyParameterResult, IMenu, string)>();

        public void Complete(ModifyParameterResult modifyParameterResult, in IMenu menu, string errorMessage = "")
        {
            _subject.OnNext((modifyParameterResult, menu, errorMessage));
        }
    }

    public class ModifyParameterUseCase : IModifyParameterUseCase
    {
        IMenuRepository _menuRepository;
        UpdateMenuSubject _updateMenuSubject;
        IModifyParameterPresenter _modifyParameterPresenter;

        public ModifyParameterUseCase(IMenuRepository menuRepository, UpdateMenuSubject updateMenuSubject, IModifyParameterPresenter modifyParameterPresenter)
        {
            _menuRepository = menuRepository;
            _updateMenuSubject = updateMenuSubject;
            _modifyParameterPresenter = modifyParameterPresenter;
        }

        public void Handle(string menuId, string modeId, int branchIndex, int parameterIndex, Parameter parameter)
        {
            try
            {
                if (menuId is null || modeId is null)
                {
                    _modifyParameterPresenter.Complete(ModifyParameterResult.ArgumentNull, null);
                    return;
                }

                if (!_menuRepository.Exists(menuId))
                {
                    _modifyParameterPresenter.Complete(ModifyParameterResult.MenuDoesNotExist, null);
                    return;
                }

                var menu = _menuRepository.Load(menuId);

                if (!menu.CanModifyParameter(modeId, branchIndex, parameterIndex))
                {
                    _modifyParameterPresenter.Complete(ModifyParameterResult.InvalidParameter, menu);
                    return;
                }

                menu.ModifyParameter(modeId, branchIndex, parameterIndex, parameter);

                _menuRepository.Save(menuId, menu, "ModifyParameter");
                _modifyParameterPresenter.Complete(ModifyParameterResult.Succeeded, menu);
                _updateMenuSubject.OnNext(menu);
            }
            catch (Exception ex)
            {
                _modifyParameterPresenter.Complete(ModifyParameterResult.Error, null, ex.ToString());
            }
        }
    }
}
