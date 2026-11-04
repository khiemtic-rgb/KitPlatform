export interface CustomerPosProfileDto {
  customerId: string;
  fullName: string;
  customerCode: string;
  ageYears?: number;
  gender?: string;
  clinicalNotes?: string;
  lastPurchaseDate?: string;
  profileGaps: string[];
}

export interface CustomerPosOrderProductDto {
  productName: string;
  quantity: number;
}

export interface CustomerPosOrderDto {
  orderDate: string;
  orderNumber: string;
  totalAmount: number;
  topProducts: CustomerPosOrderProductDto[];
}

export interface CustomerPosActiveMedicationDto {
  productName: string;
  lastPurchaseDate?: string;
  daysSupply?: number;
  supplyEndDate?: string;
  isNearEnd: boolean;
}

export interface CustomerPosConsultationDto {
  consultationDate: string;
  safetyLevel: string;
  safetyMessage?: string;
  symptoms: string[];
}

export interface CustomerPosPrescriptionDto {
  prescriptionCode: string;
  createdAt: string;
  status: string;
  prescriberName?: string | null;
  productNames?: string | null;
}

export interface CustomerPosFrequentProductDto {
  productName: string;
  orderCount: number;
  lastPurchaseDate: string;
}

export interface CustomerKnowledgeEntryDto {
  id: string;
  kind: string;
  body: string;
  sourceKind: string;
  needsVerification: boolean;
  recordedBy: string;
  recordedByName?: string | null;
  recordedAt: string;
}

export interface CustomerPosContextDto {
  profile: CustomerPosProfileDto;
  recentOrders: CustomerPosOrderDto[];
  recentLikelyActiveMedications: CustomerPosActiveMedicationDto[];
  recentConsultations: CustomerPosConsultationDto[];
  recentPrescriptions?: CustomerPosPrescriptionDto[];
  frequentPurchases?: CustomerPosFrequentProductDto[];
  knowledgeEntries?: CustomerKnowledgeEntryDto[];
  knowledgeStoreReady?: boolean;
}
