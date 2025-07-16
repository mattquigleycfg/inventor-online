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

import { detectToken, loadProfile } from './profileActions';
import notificationTypes from '../actions/notificationActions';

// prepare mock for Repository module
jest.mock('../Repository');
import repoInstance from '../Repository';

import configureMockStore from 'redux-mock-store';
import thunk from 'redux-thunk';

// mock store
const middlewares = [thunk];
const mockStore = configureMockStore(middlewares);

describe('detectToken', () => {

    let store;
    beforeEach(() => {
        store = mockStore({});
        repoInstance.setAccessToken.mockClear();
        repoInstance.forgetAccessToken.mockClear();
        delete window.location;
    });

    describe('success', () => {

        it.each([
            "?access_token=foo",
            "?first=second&access_token=foo",
        ])("should remember access token if it's in the query string (%s)",
        (queryString) => {

            window.location = { search: queryString, hash: '', pathname: '/test' };
            const replaceStateSpy = jest.spyOn(window.history, 'replaceState');

            detectToken()(store.dispatch);

            expect(repoInstance.setAccessToken).toHaveBeenCalledWith('foo');
            expect(replaceStateSpy).toHaveBeenCalled();

            replaceStateSpy.mockRestore();
        });

        it.each([
            "#access_token=foo",
            "#first=second&access_token=foo",
        ])("should remember access token if it's in the url hash (legacy support) (%s)",
        (hashString) => {

            window.location = { search: '', hash: hashString, pathname: '/test' };
            const replaceStateSpy = jest.spyOn(window.history, 'replaceState');

            detectToken()(store.dispatch);

            expect(repoInstance.setAccessToken).toHaveBeenCalledWith('foo');
            expect(replaceStateSpy).toHaveBeenCalled();

            replaceStateSpy.mockRestore();
        });

        it.each([
            "",                     // no search params
            "?",                    // query string, but nothing in it
            "?foo=1",               // different parameter
            "?access_tokennnn=1",   // slightly different name
            "?access_token=",       // expected parameter, but without value
        ])('should forget token if not found in url (%s)',
        (queryString) => {

            window.location = { search: queryString, hash: '', pathname: '/test' };

            detectToken()(store.dispatch);

            expect(repoInstance.forgetAccessToken).toHaveBeenCalled();
        });

        it('should handle errors from OAuth callback', () => {
            window.location = { search: '?error=access_denied', hash: '', pathname: '/test' };
            const replaceStateSpy = jest.spyOn(window.history, 'replaceState');

            detectToken()(store.dispatch);

            expect(repoInstance.forgetAccessToken).toHaveBeenCalled();
            
            const errorAction = store.getActions().find(a => a.type === notificationTypes.ADD_ERROR);
            expect(errorAction).toBeDefined();
            expect(errorAction.info).toContain('access_denied');
            expect(replaceStateSpy).toHaveBeenCalled();

            replaceStateSpy.mockRestore();
        });
    });

    describe('failure', () => {
        it('should log error on failure and forget access token', () => {

            // prepare to raise error during token extraction
            window.location = { search: '?access_token=foo', hash: '', pathname: '/test' };
            repoInstance.setAccessToken.mockImplementation(() => { throw new Error('123456'); });

            // execute
            detectToken()(store.dispatch);

            // check if error is logged and token is forgotten
            expect(repoInstance.setAccessToken).toHaveBeenCalled();

            const logAction = store.getActions().find(a => a.type === notificationTypes.ADD_ERROR);
            expect(logAction).toBeDefined();

            expect(repoInstance.forgetAccessToken).toHaveBeenCalled();
        });
    });
});

describe('loadProfile', () => {

    let store;
    beforeEach(() => {
        store = mockStore({});
        repoInstance.loadProfile.mockClear();
        repoInstance.hasAccessToken.mockClear();
    });

    describe('success', () => {

        it('should load profile and set logged in state', async () => {

            // prepare
            const profileMock = { name: 'John', avatarUrl: 'avatar.jpg' };
            repoInstance.loadProfile.mockResolvedValue(profileMock);
            repoInstance.hasAccessToken.mockReturnValue(true);

            // execute
            await loadProfile()(store.dispatch);

            // verify
            expect(repoInstance.loadProfile).toHaveBeenCalled();
            expect(repoInstance.hasAccessToken).toHaveBeenCalled();

            const actions = store.getActions();
            const updateProfileAction = actions.find(a => a.type === 'UPDATE_PROFILE');
            expect(updateProfileAction).toBeDefined();
            expect(updateProfileAction.profile).toEqual(profileMock);
            expect(updateProfileAction.isLoggedIn).toBe(true);
        });
    });

    describe('failure', () => {
        it('should log error on failure', async () => {

            // prepare
            const errorMock = new Error('Network error');
            repoInstance.loadProfile.mockRejectedValue(errorMock);

            // execute
            await loadProfile()(store.dispatch);

            // verify
            expect(repoInstance.loadProfile).toHaveBeenCalled();

            const actions = store.getActions();
            const errorAction = actions.find(a => a.type === notificationTypes.ADD_ERROR);
            expect(errorAction).toBeDefined();
        });
    });
});
