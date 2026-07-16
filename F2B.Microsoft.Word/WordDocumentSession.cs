using System;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    internal sealed class WordDocumentSession : IDisposable
    {
        private readonly InteropWord.Application _application;
        private readonly bool _weCreatedApplication;
        private readonly bool _attachedToAlreadyOpenDocument;
        private readonly bool _createdNewDocument;
        private readonly bool _keepOpen;
        private readonly bool _autoSaveOnComplete;
        private readonly string _wordFilePath;
        private readonly int _originalDisplayAlerts;
        private readonly bool _originalScreenUpdating;
        private InteropWord.Document _document;
        private bool _completed;
        private bool _disposed;

        private WordDocumentSession(
            InteropWord.Application application,
            InteropWord.Document document,
            bool weCreatedApplication,
            bool attachedToAlreadyOpenDocument,
            bool createdNewDocument,
            bool keepOpen,
            bool autoSaveOnComplete,
            string wordFilePath,
            int originalDisplayAlerts,
            bool originalScreenUpdating)
        {
            _application = application;
            _document = document;
            _weCreatedApplication = weCreatedApplication;
            _attachedToAlreadyOpenDocument = attachedToAlreadyOpenDocument;
            _createdNewDocument = createdNewDocument;
            _keepOpen = keepOpen;
            _autoSaveOnComplete = autoSaveOnComplete;
            _wordFilePath = wordFilePath;
            _originalDisplayAlerts = originalDisplayAlerts;
            _originalScreenUpdating = originalScreenUpdating;
        }

        internal InteropWord.Document Document => _document;

        internal static WordDocumentSession Acquire(
            string wordFilePath,
            InteropWord.Document existingDocument,
            bool visible,
            bool createIfMissing,
            bool documentArgumentBound)
        {
            var hasPath = !string.IsNullOrWhiteSpace(wordFilePath);
            if (!hasPath && existingDocument == null)
            {
                throw new ArgumentException("WordFilePath or Document is required.");
            }

            if (hasPath)
            {
                wordFilePath = WordActivityHelper.NormalizeWordFilePath(wordFilePath);
                var application = WordCom.GetOrCreateApplication(visible, out var weCreatedApplication);
                var originalDisplayAlerts = (int)application.DisplayAlerts;
                var originalScreenUpdating = application.ScreenUpdating;
                application.DisplayAlerts = (InteropWord.WdAlertLevel)WordCom.WdAlertsNone;
                application.ScreenUpdating = false;

                var document = WordCom.OpenOrCreateDocument(
                    application,
                    wordFilePath,
                    visible,
                    createIfMissing,
                    out var attachedToAlreadyOpenDocument,
                    out var createdNewDocument);

                // Keep open when Document InOut/Out is bound, or when attaching to an already-open doc
                // without an output binding (legacy path-only attach behavior).
                var keepOpen = documentArgumentBound || attachedToAlreadyOpenDocument;
                var autoSaveOnComplete = !documentArgumentBound;

                return new WordDocumentSession(
                    application,
                    document,
                    weCreatedApplication,
                    attachedToAlreadyOpenDocument,
                    createdNewDocument,
                    keepOpen,
                    autoSaveOnComplete,
                    wordFilePath,
                    originalDisplayAlerts,
                    originalScreenUpdating);
            }

            return new WordDocumentSession(
                application: null,
                document: existingDocument,
                weCreatedApplication: false,
                attachedToAlreadyOpenDocument: true,
                createdNewDocument: false,
                keepOpen: true,
                autoSaveOnComplete: false,
                wordFilePath: null,
                originalDisplayAlerts: WordCom.WdAlertsNone,
                originalScreenUpdating: true);
        }

        internal static InteropWord.Document Attach(
            string wordFilePath,
            bool visible)
        {
            wordFilePath = WordActivityHelper.NormalizeWordFilePath(wordFilePath);
            var application = WordCom.GetOrCreateApplication(visible, out _);
            application.DisplayAlerts = (InteropWord.WdAlertLevel)WordCom.WdAlertsNone;

            return WordCom.OpenOrCreateDocument(
                application,
                wordFilePath,
                visible,
                createIfMissing: false,
                out _,
                out _);
        }

        internal void Complete()
        {
            if (_completed || _document == null)
            {
                return;
            }

            _completed = true;

            if (_createdNewDocument && !string.IsNullOrWhiteSpace(_wordFilePath))
            {
                // New docs must be written once so the path exists, even when keeping open.
                WordCom.SaveAsDocx(_document, _wordFilePath);
            }
            else if (_autoSaveOnComplete)
            {
                _document.Save();
            }

            if (_keepOpen)
            {
                return;
            }

            try
            {
                _document.Close(SaveChanges: false);
            }
            finally
            {
                WordCom.ReleaseComObject(_document);
                _document = null;
            }

            if (_weCreatedApplication && _application != null)
            {
                _application.Quit(SaveChanges: false);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                if (_application != null)
                {
                    _application.DisplayAlerts = (InteropWord.WdAlertLevel)_originalDisplayAlerts;
                    _application.ScreenUpdating = _originalScreenUpdating;
                }
            }
            catch
            {
                // Ignore restore failures.
            }

            if (!_completed && _document != null && !_keepOpen)
            {
                try
                {
                    _document.Close(SaveChanges: false);
                }
                catch
                {
                    // Ignore close failures.
                }

                WordCom.ReleaseComObject(_document);
                _document = null;
            }

            if (!_completed && _weCreatedApplication && _application != null)
            {
                try
                {
                    _application.Quit(SaveChanges: false);
                }
                catch
                {
                    // Ignore quit failures.
                }

                WordCom.ReleaseComObject(_application);
            }
        }
    }
}
