using System;
using CodeHub.Core.Services;
using GitHubSharp.Models;
using System.Linq;
using CodeHub.Core.Factories;
using ReactiveUI;
using System.Reactive.Linq;
using System.Reactive;

namespace CodeHub.Core.ViewModels.Gists
{
    public class GistEditViewModel : GistModifyViewModel
    {
        private readonly ISessionService _sessionService;
        private readonly IAlertDialogFactory _alertDialogFactory;
        private DateTime _lastChanged;
        private DateTime _lastLoaded;

        private GistModel  _gist;
        public GistModel Gist
        {
            get { return _gist; }
            set { this.RaiseAndSetIfChanged(ref _gist, value); }
        }

        public GistEditViewModel(ISessionService sessionService, IAlertDialogFactory alertDialogFactory)
        {
            _sessionService = sessionService;
            _alertDialogFactory = alertDialogFactory;

            Title = "Edit Gist";

            this.WhenAnyValue(x => x.Gist)
                .IsNotNull()
                .Subscribe(x =>
                {
                    Description = x.Description;
                    foreach (var file in x.Files)
                        InternalFiles.Add(Tuple.Create(file.Key, file.Value.Content));
                    _lastLoaded = DateTime.Now;
                });

            this.WhenAnyValue(x => x.Description).Select(x => Unit.Default)
                .Merge(InternalFiles.Changed.Select(x => Unit.Default))
                .Subscribe(_ => _lastChanged = DateTime.Now);
        }

        protected override async System.Threading.Tasks.Task<GistModel> SaveGist()
        {
            if (Gist == null)
                throw new InvalidOperationException("Missing Gist context to update!");

            // We need to null out values that existed before but are not present in the update.
            var files = Gist.Files.ToDictionary(x => x.Key, x => (string)null);
            foreach (var file in Files)
                files[file.Name] = file.Content;

            var editGist = new GistEditModel
            {
                Description = Description ?? string.Empty,
                Files = files.ToDictionary(x => x.Key, x => new GistEditModel.File { Content = x.Value })
            };

            var request = _sessionService.Client.Gists[Gist.Id].EditGist(editGist);
            using (_alertDialogFactory.Activate("Updating Gist..."))
                return (await _sessionService.Client.ExecuteAsync(request)).Data;
        }

        protected override async System.Threading.Tasks.Task<bool> Discard()
        {
            if (_lastChanged <= _lastLoaded) return true;
            return await _alertDialogFactory.PromptYesNo("Discard Gist?", "Are you sure you want to discard this gist?");
        }
    }
}