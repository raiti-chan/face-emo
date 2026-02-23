using Suzuryg.FaceEmo.Domain;
using System;
using System.Collections.Generic;
using UniRx;

namespace Suzuryg.FaceEmo.UseCase.ModifyMenu.ModifyMode.ModifyBranch
{
    public interface IChangeParameterOrderUseCase
    {
        void Handle(string menuId, string modeId, int branchIndex, int from, int to);
    }

    public interface IChangeParameterOrderPresenter
    {
        IObservable<(ChangeParameterOrderResult changeParameterOrderResult, IMenu menu, string errorMessage)> Observable { get; }

        void Complete(ChangeParameterOrderResult changeParameterOrderResult, in IMenu menu, string errorMessage = "");
    }

    public enum ChangeParameterOrderResult
    {
        Succeeded,
        MenuDoesNotExist,
        InvalidParameter,
        ArgumentNull,
        Error,
    }

    public class ChangeParameterOrderPresenter : IChangeParameterOrderPresenter
    {
        public IObservable<(ChangeParameterOrderResult, IMenu, string)> Observable => _subject.AsObservable().Synchronize();

        private Subject<(ChangeParameterOrderResult, IMenu, string)> _subject = new Subject<(ChangeParameterOrderResult, IMenu, string)>();

        public void Complete(ChangeParameterOrderResult changeParameterOrderResult, in IMenu menu, string errorMessage = "")
        {
            _subject.OnNext((changeParameterOrderResult, menu, errorMessage));
        }
    }

    public class ChangeParameterOrderUseCase : IChangeParameterOrderUseCase
    {
        IMenuRepository _menuRepository;
        UpdateMenuSubject _updateMenuSubject;
        IChangeParameterOrderPresenter _changeParameterOrderPresenter;

        public ChangeParameterOrderUseCase(IMenuRepository menuRepository, UpdateMenuSubject updateMenuSubject, IChangeParameterOrderPresenter changeParameterOrderPresenter)
        {
            _menuRepository = menuRepository;
            _updateMenuSubject = updateMenuSubject;
            _changeParameterOrderPresenter = changeParameterOrderPresenter;
        }

        public void Handle(string menuId, string modeId, int branchIndex, int from, int to)
        {
            try
            {
                if (menuId is null || modeId is null)
                {
                    _changeParameterOrderPresenter.Complete(ChangeParameterOrderResult.ArgumentNull, null);
                    return;
                }

                if (!_menuRepository.Exists(menuId))
                {
                    _changeParameterOrderPresenter.Complete(ChangeParameterOrderResult.MenuDoesNotExist, null);
                    return;
                }

                var menu = _menuRepository.Load(menuId);

                if (!menu.CanChangeParameterOrder(modeId, branchIndex, from))
                {
                    _changeParameterOrderPresenter.Complete(ChangeParameterOrderResult.InvalidParameter, menu);
                    return;
                }

                menu.ChangeParameterOrder(modeId, branchIndex, from, to);

                _menuRepository.Save(menuId, menu, "ChangeParameterOrder");
                _changeParameterOrderPresenter.Complete(ChangeParameterOrderResult.Succeeded, menu);
                _updateMenuSubject.OnNext(menu);
            }
            catch (Exception ex)
            {
                _changeParameterOrderPresenter.Complete(ChangeParameterOrderResult.Error, null, ex.ToString());
            }
        }
    }
}
