/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Autodesk Design Automation team for Inventor
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

import repo from '../Repository';
import { addError, addLog } from './notificationActions';
import { showLoginFailed } from './uiFlagsActions';

export const UPDATE_PROFILE = 'UPDATE_PROFILE';

export const updateProfile = (profile, isLoggedIn) => (dispatch) => {
    dispatch({
        type: UPDATE_PROFILE,
        profile: profile,
        isLoggedIn: isLoggedIn
    });
};

/** Extract access token from URL query parameters */
function extractTokenFromQuery(urlSearch) {
    const params = new URLSearchParams(urlSearch);
    return params.get('access_token');
}

/** Extract access token from URL hash (legacy support) */
function extractTokenFromHash(urlHash) {
    const regex = /access_token=([^&]*)/g;
    const m = regex.exec(urlHash);
    return m ? m[1] : undefined;
}

/** Extract error from URL query parameters */
function extractErrorFromQuery(urlSearch) {
    const params = new URLSearchParams(urlSearch);
    return params.get('error');
}

export const detectToken = () => (dispatch) => {
    try {
        // Check for error first
        const error = extractErrorFromQuery(window.location.search);
        if (error) {
            dispatch(addError(`Authentication error: ${error}`));
            repo.forgetAccessToken();
            // Clean up URL
            window.history.replaceState({}, document.title, window.location.pathname);
            return;
        }

        // Try to extract token from query parameters (new authorization code flow)
        let accessToken = extractTokenFromQuery(window.location.search);
        
        // Fallback to hash extraction (legacy implicit flow support)
        if (!accessToken) {
            accessToken = extractTokenFromHash(window.location.hash.substring(1));
        }

        if (accessToken) {
            dispatch(addLog(`Detected access token`));
            repo.setAccessToken(accessToken);

            // Clean up URL - remove both query params and hash
            window.history.replaceState({}, document.title, window.location.pathname);
        } else {
            // Silently ignore when token is absent – app runs in anonymous Azure mode.
            repo.forgetAccessToken();
        }
    } catch (error) {
        dispatch(addError('Failed to detect token. (' + error + ')'));
        repo.forgetAccessToken();
    }
};

export const loadProfile = () => async (dispatch) => {
    dispatch(addLog('Load profile invoked'));
    try {
        const profile = await repo.loadProfile();
        dispatch(addLog('Load profile received'));
        const isLoggedIn = repo.hasAccessToken();
        dispatch(updateProfile(profile, isLoggedIn));
    } catch (error) {
        if (error.response && error.response.status === 403) {
            dispatch(showLoginFailed(true));
        } else {
            dispatch(addError('Failed to get profile. (' + error + ')'));
        }
    }
};
