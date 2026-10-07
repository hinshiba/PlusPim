import * as assert from 'assert';
import * as vscode from 'vscode';

suite('Extension Test Suite', () => {
	test('Extension is installed', () => {
		assert.ok(vscode.extensions.getExtension('hinshiba.pluspim'));
	});
});
