#include <stdio.h>
#include <Security/Authorization.h>
#include <Security/AuthorizationTags.h>

// Based on http://developer.apple.com/documentation/Security/Conceptual/authorization_concepts/03authtasks/authtasks.html#//apple_ref/doc/uid/TP30000995-CH206-TPXREF33
// Note that I tried to implement this in managed code using p/invokes, but I kept
// getting kPOSIXErrorENOENT errors on the call to AuthorizationCreate. Not sure
// why but opensnoop shows very different behavior between the managed and
// unmanaged code.
int main(int argc, char* argv[])
{
	OSStatus err;
	AuthorizationRef cookie;
	
	err = AuthorizationCreate(
		NULL,
		kAuthorizationEmptyEnvironment,
		kAuthorizationFlagDefaults,
		&cookie);
		
	if (err != errAuthorizationSuccess)
	{
		fprintf(stderr, "AuthorizationCreate failed with error %d\n", err);
		return err;
	}
	
	do
	{
		{
			AuthorizationItem items = {kAuthorizationRightExecute, 0, NULL, 0};
			AuthorizationRights rights = {1, &items};
			AuthorizationFlags flags =
				kAuthorizationFlagDefaults |
				kAuthorizationFlagInteractionAllowed |
				kAuthorizationFlagPreAuthorize |
				kAuthorizationFlagExtendRights;
			
			err = AuthorizationCopyRights(cookie, &rights, NULL, flags, NULL);
			if (err != errAuthorizationSuccess)
			{
				fprintf(stderr, "AuthorizationCopyRights failed with error %d\n", err);
				break;
			}
		}
		
		{
			char* args[] = {argv[1], argv[2], NULL};
			
			err = AuthorizationExecuteWithPrivileges(
				cookie,
				"/bin/cp",
				kAuthorizationFlagDefaults,
				args,
				NULL);
			if (err != errAuthorizationSuccess)
			{
				fprintf(stderr, "AuthorizationExecuteWithPrivileges failed with error %d\n", err);
				break;
			}
		}
	} while (0);
	
	AuthorizationFree(cookie, kAuthorizationFlagDefaults);
	
	return err;
}
