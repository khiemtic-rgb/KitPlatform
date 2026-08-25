import {
  buildCustomerProfileSnapshot,
  getCustomerProfileGaps,
  hasIncompleteCustomerProfile,
} from './consultation-customer-profile';

const fail: string[] = [];

const minimal = {
  id: 'c1',
  customerCode: 'CU001',
  fullName: 'Test',
  phone: '0900000000',
  status: 1,
  allowCredit: false,
  pharmacyRelation: 'member' as const,
};

const full = {
  ...minimal,
  dateOfBirth: '1990-05-15',
  gender: 1,
  addressLine: '123 Phố',
  clinicalNotes: 'Dị ứng penicillin',
};

if (getCustomerProfileGaps(minimal).length !== 4) fail.push('minimal gaps');
if (!hasIncompleteCustomerProfile(minimal)) fail.push('minimal incomplete');
if (hasIncompleteCustomerProfile(full)) fail.push('full complete');
const snap = buildCustomerProfileSnapshot(full);
if (snap.ageYears == null || snap.gender !== 'male') fail.push('snapshot');

if (fail.length) {
  console.error('consultation-customer-profile.smoke FAIL', fail);
  process.exit(1);
}
console.log('consultation-customer-profile.smoke OK');
