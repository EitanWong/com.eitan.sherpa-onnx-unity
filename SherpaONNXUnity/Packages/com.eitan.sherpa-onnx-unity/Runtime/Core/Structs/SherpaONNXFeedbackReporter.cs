using System;

namespace Eitan.SherpaONNXUnity.Runtime
{

    public class SherpaONNXFeedbackReporter : IProgress<IFeedback>
    {
        private readonly System.Action<IFeedback> _callback;
        private readonly ISherpaFeedbackHandler[] _visitors;
        private ILogger _logger;

        public SherpaONNXFeedbackReporter(ILogger logger = null, params ISherpaFeedbackHandler[] visitors)
        {
            // _callback = callback;
            this._visitors = visitors ?? new ISherpaFeedbackHandler[0];
            _logger = logger ?? new UnityLogger();
        }

        public SherpaONNXFeedbackReporter(System.Action<IFeedback> callback, ILogger logger = null)
        {
            this._callback = callback;
            _logger = logger ?? new UnityLogger();
        }

        public void Report(IFeedback feedback)
        {

            if (feedback == null)
            {
                throw new System.ArgumentNullException(nameof(feedback), "Feedback cannot be null");
            }

            _callback?.Invoke(feedback);

            if (_visitors == null || _visitors.Length == 0)
            { return; }
            foreach (var visitor in _visitors)
            {
                feedback.Accept(visitor);
            }
        }
    }
}
