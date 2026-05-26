using System;

namespace F2B.Terminal.PCOMM
{
    public sealed class PcommClient
    {
        private const string PresentationSpaceProgId = "PCOMM.autECLPS.1";

        public PcommSession Connect(string sessionName)
        {
            if (string.IsNullOrWhiteSpace(sessionName))
            {
                throw new ArgumentException("Session name is required.", nameof(sessionName));
            }

            var presentationSpaceType = Type.GetTypeFromProgID(PresentationSpaceProgId);
            if (presentationSpaceType == null)
            {
                throw new InvalidOperationException(
                    "PCOMM is not installed or registered. ProgID '" + PresentationSpaceProgId + "' was not found.");
            }

            // Equivalent to Python: DispatchEx("PCOMM.autECLPS.1")
            dynamic presentationSpace = Activator.CreateInstance(presentationSpaceType);

            // Equivalent to Python: ps.SetConnectionByName("A")
            presentationSpace.SetConnectionByName(sessionName);

            return new PcommSession(presentationSpace, sessionName);
        }
    }
}
