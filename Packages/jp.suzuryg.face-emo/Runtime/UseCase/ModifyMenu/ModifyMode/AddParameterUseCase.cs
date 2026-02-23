using Suzuryg.FaceEmo.Domain;
using System;
using System.Collections.Generic;
using UniRx;

namespace Suzuryg.FaceEmo.UseCase.ModifyMenu.ModifyMode.ModifyBranch
{
    public interface IAddParameterUseCase
    {
        void Handle(string menuId, string modeId, int branchIndex, Parameter parameter);
    }

    public interface IAddParameterPresenter
    {
        IObservable<(AddParameterResult addParameterResult, IMenu menu, string errorMessage)> Observable { get; }

        void Complete(AddParameterResult addParameterResult, in IMenu menu, string errorMessage = "");
    }

    public enum AddParameterResult
    {
        Succeeded,
        MenuDoesNotExist,
        InvalidBranch,
        ArgumentNull,
        Error,
    }

    public class AddParameterPresenter : IAddParameterPresenter
    {
        public IObservable<(AddParameterResult, IMenu, string)> Observable => _subject.AsObservable().Synchronize();

        private Subject<(AddParameterResult, IMenu, string)> _subject = new Subject<(AddParameterResult, IMenu, string)>();

        public void Complete(AddParameterResult addParameterResult, in IMenu menu, string errorMessage = "")
        {
            _subject.OnNext((addParameterResult, menu, errorMessage));
        }
    }

    public class AddParameterUseCase : IAddParameterUseCase
    {
        IMenuRepository _menuRepository;
        UpdateMenuSubject _updateMenuSubject;
        IAddParameterPresenter _addParameterPresenter;

        public AddParameterUseCase(IMenuRepository menuRepository, UpdateMenuSubject updateMenuSubject, IAddParameterPresenter addParameterPresenter)
        {
            _menuRepository = menuRepository;
            _updateMenuSubject = updateMenuSubject;
            _addParameterPresenter = addParameterPresenter;
        }

        public void Handle(string menuId, string modeId, int branchIndex, Parameter parameter)
        {
            try
            {
                if (menuId is null || modeId is null)
                {
                    _addParameterPresenter.Complete(AddParameterResult.ArgumentNull, null);
                    return;
                }

                if (!_menuRepository.Exists(menuId))
                {
                    _addParameterPresenter.Complete(AddParameterResult.MenuDoesNotExist, null);
                    return;
                }

                var menu = _menuRepository.Load(menuId);

                if (!menu.CanAddParameterTo(modeId, branchIndex))
                {
                    _addParameterPresenter.Complete(AddParameterResult.InvalidBranch, menu);
                    return;
                }

                menu.AddParameter(modeId, branchIndex, parameter);

                _menuRepository.Save(menuId, menu, "AddParameter");
                _addParameterPresenter.Complete(AddParameterResult.Succeeded, menu);
                _updateMenuSubject.OnNext(menu);
            }
            catch (Exception ex)
            {
                _addParameterPresenter.Complete(AddParameterResult.Error, null, ex.ToString());
            }
        }
    }
}
