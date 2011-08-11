﻿using System;
using System.Net;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGet.VisualStudio {
    public abstract class VisualStudioCredentialProvider : ICredentialProvider {
        private IVsWebProxy _webProxyService;

        public VisualStudioCredentialProvider()
            : this(ServiceLocator.GetGlobalService<SVsWebProxy, IVsWebProxy>()) {
        }

        public VisualStudioCredentialProvider(IVsWebProxy webProxyService) {
            if (webProxyService == null) {
                throw new ArgumentNullException("webProxyService");
            }
            _webProxyService = webProxyService;
        }

        public bool AllowRetry {
            get {
                return true;
            }
        }

        /// <summary>
        /// Returns an ICredentials instance that the consumer would need in order
        /// to properly authenticate to the given Uri.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="proxy"></param>
        /// <returns></returns>
        public CredentialResult GetCredentials(Uri uri, IWebProxy proxy) {
            if (uri == null) {
                throw new ArgumentNullException("uri");
            }
            // Capture the original proxy before we do anything 
            // so that we can re-set it once we get the credentials for the given Uri.
            IWebProxy originalProxy = null;
            if (proxy != null) {
                // If the current Uri should be bypassed then don't try to get the specific
                // proxy but simply capture the one that is given to us
                if (proxy.IsBypassed(uri)) {
                    originalProxy = proxy;
                }
                // If the current Uri is not bypassed then get a valid proxy for the Uri
                // and make sure that we have the credentials also.
                else {
                    originalProxy = new WebProxy(proxy.GetProxy(uri));
                    originalProxy.Credentials = proxy.Credentials == null
                                                    ? null : proxy.Credentials.GetCredential(uri, null);
                }
            }

            try {
                // The cached credentials that we found are not valid so let's ask the user
                // until they abort or give us valid credentials.
                InitializeCredentialProxy(uri, originalProxy);

                var credentialState = GetCredentials(uri, forcePrompt: true);

                // If the discovery process was aborted then reset the original proxy and exit the process.
                if (credentialState.State == CredentialState.Abort) {
                    return CredentialResult.Create(CredentialState.Abort, null);
                }

                return CredentialResult.Create(CredentialState.HasCredentials, credentialState.Credentials);

            }
            finally {
                // Reset the original WebRequest.DefaultWebProxy to what it was when we started credential discovery.
                WebRequest.DefaultWebProxy = originalProxy;
            }
        }

        /// <summary>
        /// THIS IS KINDA HACKISH: we are forcing the static property just so that the VsWebProxy can pick up the Uri.
        /// This method is responsible for initializing the WebRequest.DefaultWebProxy to the correct
        /// Uri based on the type of request that credentials are needed for before we prompt for credentials
        /// because the VsWebProxy uses that static property as a way to display the Uri that we are connecting to.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="originalProxy"></param>
        protected abstract void InitializeCredentialProxy(Uri uri, IWebProxy originalProxy);

        /// <summary>
        /// This method is responsible for retrieving either cached credentials
        /// or forcing a prompt if we need the user to give us new credentials.
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="forcePrompt"></param>
        /// <returns></returns>
        private CredentialResult GetCredentials(Uri uri, bool forcePrompt) {
            __VsWebProxyState oldState = forcePrompt
                                             ? __VsWebProxyState.VsWebProxyState_PromptForCredentials
                                             : __VsWebProxyState.VsWebProxyState_DefaultCredentials;
            var newState = (uint)__VsWebProxyState.VsWebProxyState_NoCredentials;
            int result = 0;
            ThreadHelper.Generic.Invoke(() => {
                result = _webProxyService.PrepareWebProxy(uri.OriginalString,
                                                      (uint)oldState,
                                                      out newState,
                                                      Convert.ToInt32(forcePrompt));
            });
            // If result is anything but 0 that most likely means that there was an error
            // so we will null out the DefaultWebProxy.Credentials so that we don't get
            // invalid credentials stored for subsequent requests.
            if (result != 0 || newState == (uint)__VsWebProxyState.VsWebProxyState_Abort) {
                // Clear out the current credentials because the user might have clicked cancel
                // and we don't want to use the currently set credentials if they are wrong.
                return CredentialResult.Create(CredentialState.Abort, null);
            }
            // Get the new credentials from the proxy instance
            return CredentialResult.Create(CredentialState.HasCredentials, WebRequest.DefaultWebProxy.Credentials);
        }
    }
}
