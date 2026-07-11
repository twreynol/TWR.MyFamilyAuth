using Fido2NetLib;

namespace TWR.MyFamilyAuth.API.Services;

// Fido2Configuration only holds one ServerDomain/Origins pair — since one RegisteredApp can span
// multiple effective-TLDs (e.g. a localhost dev origin and a *.fly.dev prod origin), a fresh
// Fido2Configuration/IFido2 has to be built per request rather than once at DI-registration time.
public interface IFido2Factory
{
    IFido2 Create(Fido2Configuration config);
}

public class Fido2Factory : IFido2Factory
{
    public IFido2 Create(Fido2Configuration config) => new Fido2(config);
}
