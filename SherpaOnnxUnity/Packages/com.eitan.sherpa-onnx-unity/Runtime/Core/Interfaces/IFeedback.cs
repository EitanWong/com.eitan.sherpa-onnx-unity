namespace Eitan.SherpaONNXUnity.Runtime
{

    // The base interface for all feedback types. It defines a contract for the Visitor pattern.
    public interface IFeedback
    {
        // The core of the Visitor pattern. Each feedback type will accept a visitor.
        void Accept(ISherpaFeedbackHandler handler);
    }

}
