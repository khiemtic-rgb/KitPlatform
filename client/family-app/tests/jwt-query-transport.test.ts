import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { familyEvidenceRequest } from '../src/shared/upload/bearer-transport.ts';

const token = 'jwt-family-test';

test('family image and audio evidence use bearer and omit access_token', () => {
  const image = familyEvidenceRequest(
    '/uploads/family-os/tenant/photo.jpg?access_token=secret',
    token,
  );
  assert.equal(image.authenticated, true);
  assert.equal(image.headers?.Authorization, `Bearer ${token}`);
  assert.equal(image.url.includes('access_token'), false);

  const audio = familyEvidenceRequest('/uploads/family-os/tenant/voice.webm', token);
  assert.equal(audio.authenticated, true);
  assert.equal(audio.headers?.Authorization, `Bearer ${token}`);
  assert.equal(audio.url.includes('access_token'), false);
});

test('family non-evidence media stays a direct URL', () => {
  const local = familyEvidenceRequest('blob:http://localhost/preview', token);
  assert.equal(local.authenticated, false);
  assert.equal(local.headers, undefined);
  assert.equal(local.url.includes('access_token'), false);
});

test('family http client still uses the Authorization header', () => {
  const http = readFileSync(new URL('../src/shared/api/http.ts', import.meta.url), 'utf8');
  assert.match(http, /Authorization = `Bearer \$\{token\}`/);
  assert.equal(http.includes('access_token'), false);
});
