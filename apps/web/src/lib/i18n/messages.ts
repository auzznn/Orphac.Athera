export type SupportedLocale = "id-ID" | "en-US";

export interface Dictionary {
  appName: string;
  envLabel: string;
  localeLabel: string;
  login: {
    heading: string;
    subtitle: string;
    emailLabel: string;
    emailPlaceholder: string;
    passwordLabel: string;
    rememberLabel: string;
    submitIdle: string;
    submitBusy: string;
    showPassword: string;
    hidePassword: string;
    securityNoteTitle: string;
    securityPoints: string[];
    demoHintTitle: string;
    demoHintBody: string;
    errors: Record<string, string>;
  };
  shell: {
    skipToMain: string;
    scopeUnboundBadge: string;
    scopeCompactBadge: string;
    scopeUnboundHint: string;
    scopeBoundHint: string;
    selectScope: string;
    switchScopeLabel: string;
    scopeMenuHeading: string;
    openDirectoryLink: string;
    activeTag: string;
    signOut: string;
    signingOut: string;
    logoutError: string;
    navTitle: string;
    openMenu: string;
    closeMenu: string;
    navItems: {
      overview: string;
      workspaces: string;
      invitations: string;
      partners: string;
      catalog: string;
      approvals: string;
      procurement: string;
      receiving: string;
      inventory: string;
      sales: string;
      finance: string;
      admin: string;
    };
    lockedSuffix: string;
    permittedSuffix: string;
    modulePlannedHint: string;
    sessionHeading: string;
    sessionLead: string;
    recordCardTitle: string;
    fieldUser: string;
    fieldEmail: string;
    fieldLocale: string;
    fieldLastLogin: string;
    fieldWorkspaceStatus: string;
    fieldAuthorizedCount: string;
    fieldMfaStatus: string;
    fieldDbVerification: string;
    dbVerifiedValue: string;
    boundaryTitle: string;
    boundaryBody: string;
    nextStepHeading: string;
    nextStepBodyActive: string;
    nextStepBodyEmpty: string;
    openDirectoryCta: string;
    loadingSession: string;
    errorHeading: string;
    errorRetry: string;
    errorBackToLogin: string;
  };
  mfa: {
    cardTitle: string;
    enabledBadge: string;
    disabledBadge: string;
    description: string;
    setupButton: string;
    setupBusy: string;
    sharedKeyLabel: string;
    codeLabel: string;
    codePlaceholder: string;
    verifyButton: string;
    verifyBusy: string;
    verifiedNotice: string;
    codeRequiredError: string;
  };
  invitations: {
    heading: string;
    lead: string;
    cardTitle: string;
    tokenLabel: string;
    tokenPlaceholder: string;
    submitButton: string;
    submitBusy: string;
    successTitle: string;
    successBody: string;
    enterGrantedEntity: string;
    tokenRequiredError: string;
    errors: Record<string, string>;
  };
  workspaces: {
    heading: string;
    lead: string;
    tenantCountLabel: string;
    entityCountLabel: string;
    colCode: string;
    colName: string;
    colEntity: string;
    colNpwp: string;
    colFiscal: string;
    colFiscalProfile: string;
    colGrants: string;
    colAction: string;
    enterWorkspace: string;
    activeStatus: string;
    noNpwp: string;
    emptyTitle: string;
    emptyBody: string;
    emptyIdentityRef: string;
    emptyGuidanceTitle: string;
    emptyGuidanceStep1: string;
    emptyGuidanceStep2: string;
    openInvitationCta: string;
  };
  entitySheet: {
    backToDirectory: string;
    contextCardTitle: string;
    fieldTenant: string;
    fieldEntityCode: string;
    fieldEntityName: string;
    fieldNpwp: string;
    fieldCurrency: string;
    fieldCountryTz: string;
    fieldEvaluatedAt: string;
    permissionsCardTitle: string;
    colModule: string;
    colPermission: string;
    colReadiness: string;
    availableState: string;
    permittedPlannedPrefix: string;
    notGrantedState: string;
    loadingEntity: string;
    unavailableTitle: string;
    unavailableBody: string;
    forbiddenTitle: string;
    forbiddenBody: string;
    returnWorkspaces: string;
    moduleNames: Record<string, string>;
  };
  approvals: {
    heading: string;
    lead: string;
    requestCardTitle: string;
    policyLabel: string;
    permissionLabel: string;
    reasonLabel: string;
    reasonPlaceholder: string;
    submitButton: string;
    submitBusy: string;
    casesCardTitle: string;
    emptyCases: string;
    colCase: string;
    colRequester: string;
    colPolicy: string;
    colStatus: string;
    colDecisionAction: string;
    statusPending: string;
    statusApproved: string;
    statusRejected: string;
    selfApprovalNotice: string;
    mfaRequiredTag: string;
    reasonRequiredError: string;
    decisionReasonPlaceholder: string;
    decisionReasonRequired: string;
    mfaEnrollPrompt: string;
    mfaEnrollLink: string;
    decidedPrefix: string;
    approveButton: string;
    rejectButton: string;
    decidingBusy: string;
    errors: Record<string, string>;
  };
  admin: {
    heading: string;
    lead: string;
    outboxCardTitle: string;
    outboxPending: string;
    outboxProcessed: string;
    outboxFailed: string;
    dispatchOutboxButton: string;
    dispatchBusy: string;
    membersCardTitle: string;
    colMember: string;
    colMembershipState: string;
    colGrants: string;
    colMemberActions: string;
    addPermissionLabel: string;
    grantButton: string;
    suspendButton: string;
    revokeButton: string;
    mfaActiveTag: string;
    mfaOffTag: string;
    selfLockedHint: string;
    statusLabels: Record<string, string>;
    inviteCardTitle: string;
    inviteEmailLabel: string;
    inviteEmailPlaceholder: string;
    inviteEmailRequiredError: string;
    inviteRoleLabel: string;
    inviteExpiryLabel: string;
    createInviteButton: string;
    createInviteBusy: string;
    tokenCreatedTitle: string;
    tokenCreatedNote: string;
    copyTokenButton: string;
    copiedTokenLabel: string;
    openRedeemLink: string;
    revokeInviteButton: string;
    colInviteEmail: string;
    colInviteRole: string;
    colInviteStatus: string;
    colInviteExpiry: string;
    colInviteAction: string;
    auditCardTitle: string;
    colAuditTime: string;
    colAuditEvent: string;
    colAuditActor: string;
    colAuditSummary: string;
    emptyAudit: string;
    errors: Record<string, string>;
  };
  partners: {
    heading: string;
    lead: string;
    backToEntity: string;
    readOnlyNotice: string;
    od07MetadataNotice: string;
    filterCardTitle: string;
    searchLabel: string;
    searchPlaceholder: string;
    roleFilterLabel: string;
    roleFilterAll: string;
    roleFilterSupplier: string;
    roleFilterCustomer: string;
    statusFilterLabel: string;
    statusFilterAll: string;
    eligibleOnlyLabel: string;
    createCardTitle: string;
    codeLabel: string;
    codePlaceholder: string;
    legalNameLabel: string;
    legalNamePlaceholder: string;
    displayNameLabel: string;
    displayNamePlaceholder: string;
    rolesLabel: string;
    roleSupplierCheck: string;
    roleCustomerCheck: string;
    npwpLabel: string;
    npwpPlaceholder: string;
    pkpLabel: string;
    scopeModeLabel: string;
    createButton: string;
    createBusy: string;
    listCardTitle: string;
    emptyList: string;
    colCodeName: string;
    colRoles: string;
    colScopeTax: string;
    colEligibility: string;
    colStatusAction: string;
    inspectButton: string;
    viewDetailsButton: string;
    roleBothBadge: string;
    roleSupplierBadge: string;
    roleCustomerBadge: string;
    supplierEligibleTag: string;
    customerEligibleTag: string;
    ineligibleTag: string;
    detailCardTitle: string;
    deactivateButton: string;
    activateButton: string;
    scopeAllOption: string;
    scopeRestrictedOption: string;
    updateScopeLabel: string;
    updateScopeButton: string;
    contactsTitle: string;
    emptyContacts: string;
    contactNameLabel: string;
    contactNamePlaceholder: string;
    contactRoleLabel: string;
    contactEmailLabel: string;
    contactEmailPlaceholder: string;
    contactPhoneLabel: string;
    contactPhonePlaceholder: string;
    contactPrimaryLabel: string;
    addContactButton: string;
    addContactBusy: string;
    profileTitle: string;
    profileSupplierCheck: string;
    profileCustomerCheck: string;
    profileTermsLabel: string;
    profileNotesLabel: string;
    profileNotesPlaceholder: string;
    saveProfileButton: string;
    saveProfileBusy: string;
    statusLabels: Record<string, string>;
    scopeLabels: Record<string, string>;
    pkpLabels: Record<string, string>;
    contactRoleLabels: Record<string, string>;
    validation: {
      codeRequired: string;
      legalNameRequired: string;
      roleRequired: string;
      contactNameRequired: string;
      profileTermsInvalid: string;
    };
    errors: Record<string, string>;
  };
  catalog: {
    heading: string;
    lead: string;
    backToEntity: string;
    readOnlyNotice: string;
    uomCardTitle: string;
    uomCodeLabel: string;
    uomCodePlaceholder: string;
    uomNameLabel: string;
    uomNamePlaceholder: string;
    uomScaleLabel: string;
    createUomButton: string;
    createUomBusy: string;
    scaleDiscreteHint: string;
    createItemCardTitle: string;
    itemCodeLabel: string;
    itemCodePlaceholder: string;
    itemNameLabel: string;
    itemNamePlaceholder: string;
    itemKindLabel: string;
    baseUomLabel: string;
    purchaseUomLabel: string;
    salesUomLabel: string;
    scopeModeLabel: string;
    createItemButton: string;
    createItemBusy: string;
    filterCardTitle: string;
    searchLabel: string;
    searchPlaceholder: string;
    kindFilterLabel: string;
    kindFilterAll: string;
    statusFilterLabel: string;
    statusFilterAll: string;
    eligibleOnlyLabel: string;
    listCardTitle: string;
    emptyList: string;
    colItem: string;
    colKind: string;
    colUoms: string;
    colEligibility: string;
    colAction: string;
    inspectButton: string;
    viewItemButton: string;
    purchaseEligibleTag: string;
    salesEligibleTag: string;
    ineligibleTag: string;
    detailCardTitle: string;
    deactivateButton: string;
    activateButton: string;
    toggleScopeButton: string;
    conversionsTitle: string;
    baseImplicitNote: string;
    emptyConversions: string;
    convUomLabel: string;
    convFactorLabel: string;
    convFactorPlaceholder: string;
    addConvButton: string;
    addConvBusy: string;
    converterTitle: string;
    converterUomLabel: string;
    converterQtyLabel: string;
    converterQtyPlaceholder: string;
    convertButton: string;
    convertBusy: string;
    convertResultLabel: string;
    convertRoundTripLabel: string;
    policyTitle: string;
    policyPurchaseCheck: string;
    policySalesCheck: string;
    policyNotesLabel: string;
    policyNotesPlaceholder: string;
    savePolicyButton: string;
    savePolicyBusy: string;
    statusLabels: Record<string, string>;
    kindLabels: Record<string, string>;
    scopeLabels: Record<string, string>;
    validation: {
      uomCodeRequired: string;
      uomNameRequired: string;
      itemCodeRequired: string;
      itemNameRequired: string;
      factorInvalid: string;
      quantityInvalid: string;
    };
    errors: Record<string, string>;
  };
  finance: {
    heading: string;
    lead: string;
    backToEntity: string;
    readOnlyNotice: string;
    governanceNotice: string;
    tabJournals: string;
    tabCoaMappings: string;
    tabPeriodsTax: string;
    composerTitle: string;
    draftBadge: string;
    postingDateLabel: string;
    sourceKeyLabel: string;
    sourceKeyPlaceholder: string;
    memoLabel: string;
    memoPlaceholder: string;
    lineAccountLabel: string;
    lineDescLabel: string;
    lineDescPlaceholder: string;
    lineDebitLabel: string;
    lineCreditLabel: string;
    addLineButton: string;
    removeLineLabel: string;
    totalDebitLabel: string;
    totalCreditLabel: string;
    deltaLabel: string;
    balancedTag: string;
    unbalancedTag: string;
    postJournalButton: string;
    postJournalBusy: string;
    ledgerTitle: string;
    searchPlaceholder: string;
    periodFilterAll: string;
    kindFilterAll: string;
    emptyJournals: string;
    colJournalNumber: string;
    colDatePeriod: string;
    colDescriptionSource: string;
    colDebitCredit: string;
    colStateAction: string;
    linesUnitLabel: string;
    postedBadge: string;
    reversedBadge: string;
    reversalEntryBadge: string;
    manageButton: string;
    viewOnlyButton: string;
    detailTitle: string;
    sourceTraceLabel: string;
    payloadHashLabel: string;
    viewOriginalJournalButton: string;
    viewReversalJournalButton: string;
    colLineAccount: string;
    colLineMemo: string;
    colLineDebit: string;
    colLineCredit: string;
    reverseSectionTitle: string;
    reverseDateLabel: string;
    reverseReasonLabel: string;
    reverseReasonPlaceholder: string;
    reverseButton: string;
    reverseBusy: string;
    coaTitle: string;
    addAccountTitle: string;
    accountCodeLabel: string;
    accountNameLabel: string;
    accountTypeLabel: string;
    accountCategoryLabel: string;
    parentAccountLabel: string;
    noParentOption: string;
    allowPostingCheck: string;
    controlAccountCheck: string;
    createAccountButton: string;
    createAccountBusy: string;
    headerGroupTag: string;
    postableTag: string;
    controlTag: string;
    deactivateAccountButton: string;
    activateAccountButton: string;
    mappingsTitle: string;
    colPurpose: string;
    colMappedAccount: string;
    colMappingAction: string;
    saveMappingButton: string;
    periodsTitle: string;
    addPeriodTitle: string;
    periodCodeLabel: string;
    fiscalYearLabel: string;
    periodNumberLabel: string;
    startDateLabel: string;
    endDateLabel: string;
    createPeriodButton: string;
    createPeriodBusy: string;
    periodCloseStripTitle: string;
    dateRangeMobileLabel: string;
    closePeriodNoteLabel: string;
    closePeriodNotePlaceholder: string;
    closePeriodButton: string;
    lockPeriodButton: string;
    taxCodesTitle: string;
    addTaxCodeTitle: string;
    taxCodeLabel: string;
    taxNameLabel: string;
    taxFamilyLabel: string;
    taxDirectionLabel: string;
    taxTreatmentLabel: string;
    nominalRateLabel: string;
    dppNumeratorLabel: string;
    dppDenominatorLabel: string;
    effectiveFromLabel: string;
    taxAccountLabel: string;
    noAccountOption: string;
    requiresEvidenceCheck: string;
    requiresNpwpCheck: string;
    taxNoteLabel: string;
    createTaxButton: string;
    createTaxBusy: string;
    taxPreviewTitle: string;
    selectTaxCodeOption: string;
    taxPreviewCodeLabel: string;
    taxPreviewAmountLabel: string;
    taxPreviewDateLabel: string;
    taxPreviewNpwpCheck: string;
    taxPreviewEvidenceCheck: string;
    taxPreviewButton: string;
    taxPreviewBusy: string;
    taxPreviewBaseLabel: string;
    taxPreviewTaxLabel: string;
    taxPreviewRateLabel: string;
    taxPreviewBlockedLabel: string;
    periodStatusLabels: Record<string, string>;
    accountTypeLabels: Record<string, string>;
    accountCategoryLabels: Record<string, string>;
    normalBalanceLabels: Record<string, string>;
    mappingPurposeLabels: Record<string, string>;
    taxFamilyLabels: Record<string, string>;
    taxDirectionLabels: Record<string, string>;
    taxTreatmentLabels: Record<string, string>;
    taxReviewLabels: Record<string, string>;
    validation: {
      memoRequired: string;
      sourceKeyRequired: string;
      postingDateRequired: string;
      accountCodeRequired: string;
      accountNameRequired: string;
      periodCodeRequired: string;
      periodYearInvalid: string;
      periodNumberInvalid: string;
      periodDatesInvalid: string;
      taxCodeRequired: string;
      taxNameRequired: string;
      taxRateInvalid: string;
      taxDppInvalid: string;
      taxEffDateRequired: string;
      taxPreviewAmountInvalid: string;
      reversalDateRequired: string;
      reversalReasonRequired: string;
    };
    errors: Record<string, string>;
  };
  procurement: {
    pageTitle: string;
    backToOverview: string;
    loadErrorTitle: string;
    retryButton: string;
    readOnlyBanner: string;
    governanceNotice: string;
    tabListLabel: string;
    tabs: {
      list: string;
      editor: string;
    };
    searchPlaceholder: string;
    allStatusesOption: string;
    poListHeading: string;
    emptyList: string;
    colPoNumber: string;
    colSupplier: string;
    colDates: string;
    colRevision: string;
    colTotals: string;
    colStatus: string;
    colAction: string;
    orderDateShort: string;
    expectedDateShort: string;
    revisionPrefix: string;
    openDraftBadge: string;
    openQtySuffix: string;
    manageActionButton: string;
    pendingSelfActionButton: string;
    reviewActionButton: string;
    readOnlyActionButton: string;
    selectPoPrompt: string;
    detailHeadingPrefix: string;
    snapshotHashLabel: string;
    destinationLabel: string;
    termsLabel: string;
    termsDaysSuffix: string;
    toleranceLabel: string;
    createdByLabel: string;
    submittedByLabel: string;
    decidedByLabel: string;
    changeReasonLabel: string;
    notesLabel: string;
    commitmentHeading: string;
    orderedBaseLabel: string;
    acceptedBaseLabel: string;
    remainingOpenLabel: string;
    receivingProgressLabel: string;
    downstreamRefsLabel: string;
    revisionHistoryLabel: string;
    frozenBadge: string;
    editableBadge: string;
    lineTableHeading: string;
    colLineNo: string;
    colItem: string;
    colQtyUom: string;
    colBaseConversion: string;
    colUnitPrice: string;
    colTax: string;
    colLineTotal: string;
    subtotalLabel: string;
    ppnTotalLabel: string;
    whtTotalLabel: string;
    grandTotalLabel: string;
    diffHeading: string;
    diffHeaderTitle: string;
    diffLinesTitle: string;
    diffTotalDeltaLabel: string;
    diffBaseQtyDeltaLabel: string;
    diffNoHeaderChanges: string;
    colDiffLineKey: string;
    colDiffChange: string;
    colDiffPrevious: string;
    colDiffCurrent: string;
    colDiffSubtotal: string;
    stickyBarHeading: string;
    selfApprovalBlockedBanner: string;
    pendingApprovalNote: string;
    decisionReasonLabel: string;
    decisionReasonPlaceholder: string;
    approveRevisionButton: string;
    approveRevisionBusy: string;
    rejectRevisionButton: string;
    rejectRevisionBusy: string;
    editDraftButton: string;
    submitRevisionButton: string;
    submitRevisionBusy: string;
    amendmentHeading: string;
    amendmentReasonLabel: string;
    amendmentReasonPlaceholder: string;
    createAmendmentButton: string;
    createAmendmentBusy: string;
    shortCloseHeading: string;
    closeReasonLabel: string;
    closeReasonPlaceholder: string;
    shortCloseButton: string;
    shortCloseBusy: string;
    cancelPoHeading: string;
    cancelReasonLabel: string;
    cancelReasonPlaceholder: string;
    cancelPoButton: string;
    cancelPoBusy: string;
    cancelBlockedNote: string;
    terminalPoControlNote: string;
    editorCreateTitle: string;
    editorEditDraftTitle: string;
    supplierLabel: string;
    selectSupplierOption: string;
    orderDateLabel: string;
    expectedDateLabel: string;
    destinationInputLabel: string;
    destinationPlaceholder: string;
    termsInputLabel: string;
    toleranceSelectLabel: string;
    editorNotesLabel: string;
    editorNotesPlaceholder: string;
    editorChangeReasonLabel: string;
    editorChangeReasonPlaceholder: string;
    linesSectionHeading: string;
    addLineButton: string;
    removeLineButton: string;
    lineItemLabel: string;
    selectItemOption: string;
    lineUomLabel: string;
    lineQtyLabel: string;
    linePriceLabel: string;
    lineTaxLabel: string;
    noTaxOption: string;
    lineDescLabel: string;
    lineDescPlaceholder: string;
    linePreviewBasePrefix: string;
    linePreviewTaxPrefix: string;
    od01NoticeLine: string;
    saveDraftOnlyButton: string;
    saveAndSubmitButton: string;
    editorBusy: string;
    cancelEditorButton: string;
    poStatusLabels: Record<string, string>;
    revisionStatusLabels: Record<string, string>;
    receivingProgressLabels: Record<string, string>;
    diffChangeLabels: Record<string, string>;
    headerFieldLabels: Record<string, string>;
    itemKindLabels: Record<string, string>;
    validation: {
      supplierRequired: string;
      orderDateRequired: string;
      expectedDateBeforeOrder: string;
      destinationRequired: string;
      paymentTermsInvalid: string;
      linesRequired: string;
      lineItemRequired: string;
      lineQtyInvalid: string;
      linePriceInvalid: string;
      decisionReasonRequired: string;
      amendmentReasonRequired: string;
      closeReasonRequired: string;
      cancelReasonRequired: string;
    };
    errors: Record<string, string>;
  };
  receiving: {
    pageTitle: string;
    backToOverview: string;
    loadErrorTitle: string;
    retryButton: string;
    readOnlyBanner: string;
    gateOneZeroValuationBanner: string;
    od01PolicyBanner: string;
    tabListLabel: string;
    tabs: {
      ledger: string;
      arrival: string;
    };
    stats: {
      totalReceipts: string;
      deliveredBase: string;
      heldBase: string;
      releasedBase: string;
      rejectedBase: string;
      linkedGlJournals: string;
    };
    filterSearchLabel: string;
    filterSearchPlaceholder: string;
    filterStatusLabel: string;
    allStatusesOption: string;
    refreshButton: string;
    registerCardTitle: string;
    emptyLedgerTitle: string;
    emptyLedgerBody: string;
    colReceipt: string;
    colPoAndSupplier: string;
    colStationAndDate: string;
    colQuantities: string;
    colStatus: string;
    colAction: string;
    openDetailButton: string;
    activeDetailButton: string;
    arrivalCardTitle: string;
    arrivalNoEligiblePosTitle: string;
    arrivalNoEligiblePosBody: string;
    arrivalOpenPoModuleHint: string;
    poSelectorLabel: string;
    poSelectorPlaceholder: string;
    deliveryNoteLabel: string;
    deliveryNotePlaceholder: string;
    receivedDateLabel: string;
    receivingStationLabel: string;
    receivingStationPlaceholder: string;
    vehicleRefLabel: string;
    vehicleRefPlaceholder: string;
    documentRefLabel: string;
    documentRefPlaceholder: string;
    arrivalNotesLabel: string;
    arrivalNotesPlaceholder: string;
    arrivalLinesHeading: string;
    colPoLine: string;
    colOrderedRemaining: string;
    colDeliveredQty: string;
    colArrivalFlags: string;
    deliveredQtyPlaceholder: string;
    uomSelectLabel: string;
    flagForInspectionLabel: string;
    discrepancyReasonLabel: string;
    discrepancyNotesPlaceholder: string;
    conditionEvidencePlaceholder: string;
    arrivalConvertedBasePrefix: string;
    arrivalOd01ExcessPreview: string;
    submitArrivalButton: string;
    submitArrivalBusy: string;
    arrivalSuccessPrefix: string;
    arrivalReplaySuffix: string;
    detailCardTitle: string;
    selectReceiptPromptTitle: string;
    selectReceiptPromptBody: string;
    fieldReceiptNumber: string;
    fieldPoRef: string;
    fieldSupplier: string;
    fieldDeliveryNote: string;
    fieldStation: string;
    fieldVehicleRef: string;
    fieldDocumentRef: string;
    fieldFinancialPosting: string;
    noGlPostingBadge: string;
    fieldReceivedBy: string;
    fieldLineCustody: string;
    colCustodyLine: string;
    colBaseSplit: string;
    colValuedAccepted: string;
    colOd01Excess: string;
    dockCheckFormHeading: string;
    dockCheckReadOnlyNote: string;
    conservationFormulaLabel: string;
    conservationBalancedTag: string;
    conservationMismatchTag: string;
    od01CapLabel: string;
    od01CapExceededTag: string;
    presetReleaseWithinCap: string;
    presetHoldAll: string;
    presetRejectAll: string;
    pendingQtyLabel: string;
    heldQtyLabel: string;
    releasedQtyLabel: string;
    rejectedQtyLabel: string;
    inspectionNotesLabel: string;
    inspectionNotesPlaceholder: string;
    evidenceRefLabel: string;
    evidenceRefPlaceholder: string;
    submitDockCheckButton: string;
    submitDockCheckBusy: string;
    dockCheckSuccessBanner: string;
    historyCardTitle: string;
    emptyHistoryBody: string;
    colCheckedAt: string;
    colTransition: string;
    colSplitSnapshot: string;
    colReasonAndActor: string;
    receiptStatusLabels: Record<string, string>;
    custodyStatusLabels: Record<string, string>;
    discrepancyReasonLabels: Record<string, string>;
    defaultReceivingStation: string;
    hashPrefix: string;
    remainingCapPrefix: string;
    capPrefix: string;
    discrepancyNotesLabel: string;
    conditionEvidenceLabel: string;
    splitCodes: {
      delivered: string;
      pending: string;
      held: string;
      released: string;
      rejected: string;
    };
    validation: {
      poRequired: string;
      deliveryNoteRequired: string;
      receivedDateRequired: string;
      stationRequired: string;
      arrivalLinesRequired: string;
      invalidQtyFormat: string;
      conservationMismatch: string;
      discrepancyReasonRequired: string;
    };
    errors: Record<string, string>;
  };
}

export const MESSAGES: Record<SupportedLocale, Dictionary> = {
  "id-ID": {
    appName: "Orphac Athera",
    envLabel: "Fondasi Terverifikasi · PostgreSQL",
    localeLabel: "Bahasa",
    login: {
      heading: "Masuk ke Sistem Operasional",
      subtitle: "Gunakan akun ASP.NET Core Identity yang terdaftar pada direktori organisasi Anda.",
      emailLabel: "Email Perusahaan",
      emailPlaceholder: "nama@perusahaan.co.id",
      passwordLabel: "Kata Sandi",
      rememberLabel: "Pertahankan sesi pada peramban ini (8 jam bergulir)",
      submitIdle: "Masuk ke Athera",
      submitBusy: "Memverifikasi kredensial...",
      showPassword: "Tampilkan kata sandi",
      hidePassword: "Sembunyikan kata sandi",
      securityNoteTitle: "Batas Keamanan Sesi",
      securityPoints: [
        "Kredensial hanya diverifikasi oleh server ASP.NET Core Identity.",
        "Cookie sesi HttpOnly dengan perlindungan CSRF pada setiap aksi mutasi.",
        "Akses data tenant dan entitas hukum diisolasi per pengguna oleh PostgreSQL RLS.",
      ],
      demoHintTitle: "Lingkungan Uji Lokal (Data Sintetis)",
      demoHintBody: "Untuk verifikasi pengembangan lokal gunakan akun demo ter-seed (@demo.athera.local).",
      errors: {
        invalid_credentials: "Email atau kata sandi tidak sesuai. Periksa kembali kredensial Anda.",
        account_locked: "Akun dikunci sementara setelah beberapa kali gagal masuk. Tunggu beberapa menit.",
        account_inactive: "Akun Anda dinonaktifkan. Hubungi Administrator Organisasi.",
        csrf_validation_failed: "Token keamanan formulir kedaluwarsa. Muat ulang dan coba lagi.",
        validation_error: "Isi alamat email dan kata sandi terlebih dahulu.",
        upstream_unavailable: "Layanan API backend tidak dapat dijangkau. Pastikan API berjalan.",
        unknown: "Gagal memproses permintaan masuk. Coba beberapa saat lagi.",
      },
    },
    shell: {
      skipToMain: "Lewati ke konten utama",
      scopeUnboundBadge: "Tanpa Ruang Kerja Aktif (Default-Deny)",
      scopeCompactBadge: "Belum ada ruang kerja aktif",
      scopeUnboundHint: "Hubungi Administrator Organisasi untuk keanggotaan tenant dan izin entitas.",
      scopeBoundHint: "Konteks tenant dan entitas dikunci server melalui PostgreSQL RLS per transaksi.",
      selectScope: "Pilih Entitas",
      switchScopeLabel: "Ganti ruang kerja entitas",
      scopeMenuHeading: "Entitas Hukum Diotorisasi",
      openDirectoryLink: "Buka Direktori Ruang Kerja",
      activeTag: "Aktif",
      signOut: "Keluar",
      signingOut: "Mengakhiri sesi...",
      logoutError: "Gagal mengakhiri sesi dengan aman. Coba lagi.",
      navTitle: "Modul Operasional",
      openMenu: "Buka menu navigasi",
      closeMenu: "Tutup menu navigasi",
      navItems: {
        overview: "Ringkasan Sesi",
        workspaces: "Ruang Kerja Entitas",
        invitations: "Terima Undangan",
        partners: "Mitra Bisnis",
        catalog: "Katalog & Satuan",
        approvals: "Persetujuan",
        procurement: "Pengadaan",
        receiving: "Penerimaan Gudang",
        inventory: "Persediaan",
        sales: "Penjualan",
        finance: "Keuangan & GL",
        admin: "Administrasi",
      },
      lockedSuffix: "Tidak diotorisasi pada entitas ini",
      permittedSuffix: "Izin aktif; modul dibuka pada slice berikutnya",
      modulePlannedHint: "Segera",
      sessionHeading: "Ringkasan Identitas & Otorisasi Sesi",
      sessionLead: "Status akun dan ruang kerja aktif dimuat langsung dari PostgreSQL dengan isolasi peran minimum.",
      recordCardTitle: "Rekam Identitas PostgreSQL",
      fieldUser: "Nama Tampilan",
      fieldEmail: "Email Terdaftar",
      fieldLocale: "Preferensi Bahasa",
      fieldLastLogin: "Masuk Terakhir (WIB)",
      fieldWorkspaceStatus: "Status Ruang Kerja",
      fieldAuthorizedCount: "Entitas Diotorisasi",
      fieldMfaStatus: "Autentikator MFA (TOTP)",
      fieldDbVerification: "Koneksi & Peran Basis Data",
      dbVerifiedValue: "Terhubung ke PostgreSQL (peran runtime non-superuser + RLS aktif)",
      boundaryTitle: "Batas Keamanan Multi-Tenant Aktif",
      boundaryBody:
        "Setiap permintaan ruang kerja memvalidasi keanggotaan dan izin entitas di server sebelum mengunci app.tenant_id lokal transaksi.",
      nextStepHeading: "Ruang Kerja Entitas Hukum",
      nextStepBodyActive:
        "Buka direktori ruang kerja untuk masuk ke entitas hukum yang Anda miliki atau berpindah konteks secara aman.",
      nextStepBodyEmpty:
        "Akun ini belum memiliki keanggotaan tenant atau izin entitas aktif. Gunakan token undangan atau hubungi admin organisasi.",
      openDirectoryCta: "Buka Direktori Ruang Kerja",
      loadingSession: "Memeriksa sesi aktif...",
      errorHeading: "Gagal Memverifikasi Sesi",
      errorRetry: "Coba Lagi",
      errorBackToLogin: "Kembali ke Masuk",
    },
    mfa: {
      cardTitle: "Keamanan MFA TOTP (Persetujuan Berisiko Tinggi)",
      enabledBadge: "MFA Aktif",
      disabledBadge: "MFA Belum Aktif",
      description: "Diwajibkan untuk menyetujui kebijakan persetujuan berisiko tinggi pada entitas.",
      setupButton: "Siapkan Kunci TOTP",
      setupBusy: "Membuat kunci...",
      sharedKeyLabel: "Kunci Rahasia Authenticator (Base32)",
      codeLabel: "Kode 6 Digit Authenticator",
      codePlaceholder: "123456",
      verifyButton: "Verifikasi & Aktifkan MFA",
      verifyBusy: "Memverifikasi...",
      verifiedNotice: "MFA TOTP aktif untuk akun ini.",
      codeRequiredError: "Masukkan kode 6 digit dari aplikasi Authenticator.",
    },
    invitations: {
      heading: "Terima Undangan Organisasi",
      lead: "Masukkan token undangan sekali pakai dari Administrator Organisasi menggunakan akun email yang cocok.",
      cardTitle: "Aktivasi Undangan Entitas Hukum",
      tokenLabel: "Token Undangan",
      tokenPlaceholder: "Tempel token undangan di sini",
      submitButton: "Terima Undangan & Aktifkan Akses",
      submitBusy: "Memvalidasi undangan...",
      successTitle: "Undangan Diterima",
      successBody: "Keanggotaan dan izin entitas baru telah diaktifkan tanpa mengaktifkan ulang izin lama yang pernah dicabut.",
      enterGrantedEntity: "Masuk ke Entitas Sekarang",
      tokenRequiredError: "Masukkan token undangan sekali pakai dari administrator.",
      errors: {
        invitation_not_found: "Token undangan tidak dikenali.",
        invitation_expired: "Undangan telah kedaluwarsa. Minta administrator membuat undangan baru.",
        invitation_revoked: "Undangan telah dicabut oleh administrator.",
        invitation_already_accepted: "Undangan ini sudah digunakan sebelumnya.",
        invitation_email_mismatch: "Email akun yang sedang masuk tidak cocok dengan penerima undangan.",
      },
    },
    workspaces: {
      heading: "Direktori Ruang Kerja",
      lead: "Hanya tenant dan entitas hukum dengan keanggotaan serta izin aktif yang ditampilkan untuk akun Anda.",
      tenantCountLabel: "Tenant aktif",
      entityCountLabel: "Entitas diotorisasi",
      colCode: "Kode Entitas",
      colName: "Nama Entitas Hukum",
      colEntity: "Entitas Hukum",
      colNpwp: "NPWP",
      colFiscal: "Mata Uang & Zona Waktu",
      colFiscalProfile: "NPWP & Konteks Fiskal",
      colGrants: "Izin Aktif",
      colAction: "Tindakan",
      enterWorkspace: "Masuk Ruang Kerja",
      activeStatus: "Aktif",
      noNpwp: "Belum diisi",
      emptyTitle: "Belum Ada Ruang Kerja yang Diotorisasi",
      emptyBody:
        "Identitas ASP.NET Core Identity Anda terverifikasi, namun belum memiliki keanggotaan tenant maupun izin entitas hukum yang aktif. Akses bersifat ditolak secara default.",
      emptyIdentityRef: "Referensi Akun Terautentikasi",
      emptyGuidanceTitle: "Cara Memperoleh Akses Ruang Kerja",
      emptyGuidanceStep1: "Minta Administrator Organisasi membuat undangan pada menu Administrasi entitas untuk email akun Anda.",
      emptyGuidanceStep2: "Setelah menerima token undangan, buka halaman Terima Undangan untuk mengaktifkan akses entitas.",
      openInvitationCta: "Masukkan Token Undangan",
    },
    entitySheet: {
      backToDirectory: "Direktori Ruang Kerja",
      contextCardTitle: "Profil Entitas Hukum & Konteks Fiskal",
      fieldTenant: "Tenant Induk",
      fieldEntityCode: "Kode Entitas",
      fieldEntityName: "Nama Entitas Hukum",
      fieldNpwp: "NPWP Entitas",
      fieldCurrency: "Mata Uang Fungsional",
      fieldCountryTz: "Yurisdiksi & Zona Waktu",
      fieldEvaluatedAt: "Verifikasi Server (WIB)",
      permissionsCardTitle: "Izin Efektif & Kesiapan Modul",
      colModule: "Modul Operasional",
      colPermission: "Kode Izin",
      colReadiness: "Status Kesiapan",
      availableState: "Tersedia (S01–S06)",
      permittedPlannedPrefix: "Diizinkan · Tahap",
      notGrantedState: "Tidak Diizinkan",
      loadingEntity: "Memuat ruang kerja entitas terisolasi...",
      unavailableTitle: "Ruang Kerja Tidak Tersedia",
      unavailableBody:
        "Tenant atau entitas hukum yang diminta tidak ditemukan atau berada di luar otorisasi aktif akun Anda.",
      forbiddenTitle: "Izin Entitas Tidak Mencukupi",
      forbiddenBody:
        "Anda diotorisasi pada entitas ini, namun izin khusus yang diminta tidak tersedia pada penugasan Anda.",
      returnWorkspaces: "Kembali ke Direktori Ruang Kerja",
      moduleNames: {
        overview: "Ringkasan Entitas & Tata Kelola",
        partners: "Mitra Pemasok & Pelanggan",
        catalog: "Katalog Barang & Satuan UoM",
        approvals: "Kotak Masuk & Persetujuan Empat Mata",
        procurement: "Pengadaan (PO, GRN, Pemasok)",
        receiving: "Penerimaan Dermaga & Gate 1 (Tanpa Valuasi)",
        inventory: "Persediaan & Buku Besar Stok",
        sales: "Penjualan & Penagihan",
        finance: "Keuangan, Pajak PPN/PPh & GL",
        admin: "Administrasi Organisasi, Undangan & Audit",
      },
    },
    approvals: {
      heading: "Kotak Masuk Persetujuan Empat Mata",
      lead: "Ajukan permintaan izin entitas atau putuskan permintaan tertunda secara independen (OD-02: pengaju tidak boleh menyetujui permintaannya sendiri).",
      requestCardTitle: "Ajukan Permintaan Izin Entitas",
      policyLabel: "Kebijakan Persetujuan",
      permissionLabel: "Izin yang Diminta",
      reasonLabel: "Justifikasi Operasional",
      reasonPlaceholder: "Contoh: Perlu akses stok Q4",
      submitButton: "Kirim Permintaan Persetujuan",
      submitBusy: "Mengirim permintaan...",
      casesCardTitle: "Daftar Kasus Persetujuan Entitas",
      emptyCases: "Belum ada kasus persetujuan pada entitas ini.",
      colCase: "Permintaan & Kebijakan",
      colRequester: "Pengaju & Pemutus",
      colPolicy: "Aturan Kebijakan",
      colStatus: "Status Keputusan",
      colDecisionAction: "Keputusan Independen",
      statusPending: "Menunggu Keputusan",
      statusApproved: "Disetujui",
      statusRejected: "Ditolak",
      selfApprovalNotice: "Aturan Empat Mata (OD-02): Anda adalah pengaju kasus ini; persetujuan hanya dapat dilakukan oleh pemutus lain.",
      mfaRequiredTag: "Wajib MFA",
      reasonRequiredError: "Tuliskan justifikasi operasional sebelum mengirim permintaan.",
      decisionReasonPlaceholder: "Alasan persetujuan / penolakan...",
      decisionReasonRequired: "Tuliskan alasan keputusan sebelum menyetujui atau menolak.",
      mfaEnrollPrompt: "Kebijakan ini mewajibkan MFA TOTP aktif sebelum Anda dapat memutuskan permintaan.",
      mfaEnrollLink: "Aktifkan MFA di halaman Akun",
      decidedPrefix: "Diputuskan",
      approveButton: "Setujui",
      rejectButton: "Tolak",
      decidingBusy: "Memproses...",
      errors: {
        four_eyes_self_approval_forbidden: "Aturan Empat Mata melarang menyetujui permintaan Anda sendiri.",
        mfa_required: "Kebijakan ini mewajibkan akun pemutus mengaktifkan MFA TOTP terlebih dahulu.",
        case_already_decided: "Kasus persetujuan ini sudah diputuskan sebelumnya.",
      },
    },
    admin: {
      heading: "Administrasi Entitas, Undangan & Audit",
      lead: "Kelola anggota tenant, undangan sekali pakai, izin per entitas, log audit append-only, dan pengiriman outbox.",
      outboxCardTitle: "Status Outbox Transaksional Tenant",
      outboxPending: "Tertunda",
      outboxProcessed: "Diproses",
      outboxFailed: "Gagal",
      dispatchOutboxButton: "Proses Antrean Outbox",
      dispatchBusy: "Memproses outbox...",
      membersCardTitle: "Anggota Tenant & Izin Entitas",
      colMember: "Pengguna & MFA",
      colMembershipState: "Status Keanggotaan",
      colGrants: "Izin Entitas Ini",
      colMemberActions: "Tindakan Administrasi",
      addPermissionLabel: "Tambah izin",
      grantButton: "Berikan",
      suspendButton: "Tangguhkan",
      revokeButton: "Cabut Akses",
      mfaActiveTag: "MFA Aktif",
      mfaOffTag: "Tanpa MFA",
      selfLockedHint: "Akun sendiri",
      statusLabels: {
        ACTIVE: "Aktif",
        SUSPENDED: "Ditangguhkan",
        REVOKED: "Dicabut",
        PENDING: "Menunggu",
        ACCEPTED: "Diterima",
        EXPIRED: "Kedaluwarsa",
      },
      inviteCardTitle: "Undang Pengguna ke Entitas Ini",
      inviteEmailLabel: "Email Penerima",
      inviteEmailPlaceholder: "staf@demo.athera.local",
      inviteEmailRequiredError: "Masukkan alamat email penerima yang valid.",
      inviteRoleLabel: "Templat Peran",
      inviteExpiryLabel: "Masa Berlaku (Jam)",
      createInviteButton: "Buat Undangan",
      createInviteBusy: "Membuat undangan...",
      tokenCreatedTitle: "Token Undangan Sekali Pakai Dibuat",
      tokenCreatedNote: "Salin token atau tautan ini sekarang (server hanya menyimpan hash SHA-256):",
      copyTokenButton: "Salin Token",
      copiedTokenLabel: "Tersalin",
      openRedeemLink: "Buka halaman penukaran token",
      revokeInviteButton: "Cabut Undangan",
      colInviteEmail: "Email Undangan",
      colInviteRole: "Peran",
      colInviteStatus: "Status",
      colInviteExpiry: "Kedaluwarsa (WIB)",
      colInviteAction: "Tindakan",
      auditCardTitle: "Jejak Audit Append-Only Entitas",
      colAuditTime: "Waktu (WIB)",
      colAuditEvent: "Peristiwa",
      colAuditActor: "Aktor",
      colAuditSummary: "Ringkasan",
      emptyAudit: "Belum ada catatan audit tercatat.",
      errors: {
        invalid_permission_code: "Kode izin tidak terdaftar dalam katalog resmi.",
        grant_already_exists: "Izin tersebut sudah aktif dimiliki pengguna.",
      },
    },
    partners: {
      heading: "Mitra Bisnis (Pemasok & Pelanggan)",
      lead: "Data induk mitra dipakai bersama per tenant, sedangkan kelayakan operasional dikunci per entitas hukum.",
      backToEntity: "Ringkasan Entitas",
      readOnlyNotice: "Mode baca saja: Anda memiliki izin lihat (partners.read) tanpa hak ubah (partners.manage).",
      od07MetadataNotice: "Catatan kebijakan OD-07: NPWP dan status PKP dicatat sebagai metadata master saja; perhitungan tarif PPN/PPh ditangani terpisah pada modul pajak (S04+).",
      filterCardTitle: "Cari & Saring Mitra Bisnis",
      searchLabel: "Cari Kode / Nama",
      searchPlaceholder: "Contoh: PT-DUAL-01 atau Mitra Niaga",
      roleFilterLabel: "Peran Mitra",
      roleFilterAll: "Semua Peran",
      roleFilterSupplier: "Pemasok",
      roleFilterCustomer: "Pelanggan",
      statusFilterLabel: "Status",
      statusFilterAll: "Semua Status",
      eligibleOnlyLabel: "Hanya layak di entitas ini",
      createCardTitle: "Daftarkan Mitra Bisnis Baru",
      codeLabel: "Kode Mitra",
      codePlaceholder: "PT-DAGANG-01",
      legalNameLabel: "Nama Badan Hukum",
      legalNamePlaceholder: "PT Dagang Serba Usaha Nusantara",
      displayNameLabel: "Nama Tampilan",
      displayNamePlaceholder: "Dagang Serba Usaha",
      rolesLabel: "Peran Transaksi",
      roleSupplierCheck: "Pemasok (Supplier)",
      roleCustomerCheck: "Pelanggan (Customer)",
      npwpLabel: "NPWP (Opsional)",
      npwpPlaceholder: "01.333.444.5-011.000",
      pkpLabel: "Metadata PKP",
      scopeModeLabel: "Cakupan Entitas",
      createButton: "Simpan Mitra Baru",
      createBusy: "Menyimpan mitra...",
      listCardTitle: "Daftar Mitra Bisnis Tenant",
      emptyList: "Tidak ada mitra yang sesuai dengan filter.",
      colCodeName: "Kode & Nama Mitra",
      colRoles: "Peran",
      colScopeTax: "Cakupan & Metadata PKP",
      colEligibility: "Kelayakan Entitas Aktif",
      colStatusAction: "Status & Rincian",
      inspectButton: "Kelola Rincian",
      viewDetailsButton: "Lihat Rincian",
      roleBothBadge: "Pemasok & Pelanggan",
      roleSupplierBadge: "Pemasok",
      roleCustomerBadge: "Pelanggan",
      supplierEligibleTag: "Bisa Beli (AP)",
      customerEligibleTag: "Bisa Jual (AR)",
      ineligibleTag: "Tidak Layak di Entitas Ini",
      detailCardTitle: "Rincian Mitra, Kontak & Kelayakan Entitas",
      deactivateButton: "Nonaktifkan Mitra",
      activateButton: "Aktifkan Kembali",
      scopeAllOption: "Semua Entitas Aktif",
      scopeRestrictedOption: "Entitas Terpilih Saja",
      updateScopeLabel: "Ubah Mode Cakupan",
      updateScopeButton: "Terapkan Mode Cakupan",
      contactsTitle: "Daftar Kontak Mitra",
      emptyContacts: "Belum ada kontak terdaftar untuk mitra ini.",
      contactNameLabel: "Nama Kontak",
      contactNamePlaceholder: "Dian Permata",
      contactRoleLabel: "Peran Kontak",
      contactEmailLabel: "Email Kontak",
      contactEmailPlaceholder: "dian@mitra.co.id",
      contactPhoneLabel: "Telepon",
      contactPhonePlaceholder: "+62-21-555-0199",
      contactPrimaryLabel: "Kontak utama",
      addContactButton: "Tambah Kontak",
      addContactBusy: "Menambah kontak...",
      profileTitle: "Kelayakan Entitas Hukum Saat Ini",
      profileSupplierCheck: "Izinkan sebagai Pemasok pada entitas ini",
      profileCustomerCheck: "Izinkan sebagai Pelanggan pada entitas ini",
      profileTermsLabel: "Termin Pembayaran (Hari)",
      profileNotesLabel: "Catatan Kebijakan Entitas",
      profileNotesPlaceholder: "Contoh: Pemasok resmi gudang Jakarta",
      saveProfileButton: "Simpan Profil Entitas",
      saveProfileBusy: "Menyimpan profil...",
      statusLabels: {
        ACTIVE: "Aktif",
        INACTIVE: "Nonaktif",
      },
      scopeLabels: {
        ALL_ENTITIES: "Semua Entitas",
        RESTRICTED_ENTITIES: "Terbatas per Entitas",
      },
      pkpLabels: {
        UNKNOWN: "Belum Diketahui",
        PKP: "PKP (Metadata)",
        NON_PKP: "Non-PKP",
      },
      contactRoleLabels: {
        COMMERCIAL: "Komersial / Sales",
        BILLING: "Penagihan / Pajak",
        RECEIVING_DISPATCH: "Gudang / Penerimaan",
      },
      validation: {
        codeRequired: "Masukkan kode mitra (2–32 karakter huruf besar/angka).",
        legalNameRequired: "Masukkan nama badan hukum mitra.",
        roleRequired: "Pilih minimal satu peran: Pemasok atau Pelanggan.",
        contactNameRequired: "Masukkan nama kontak terlebih dahulu.",
        profileTermsInvalid: "Termin pembayaran harus bilangan bulat 0–365 hari.",
      },
      errors: {
        duplicate_partner_code: "Kode mitra sudah digunakan dalam tenant ini.",
        partner_role_incompatible: "Kelayakan entitas tidak boleh melampaui peran dasar mitra (Pemasok/Pelanggan).",
        concurrency_conflict: "Data mitra diubah pengguna lain. Muat ulang terlebih dahulu.",
        insufficient_entity_permission: "Anda tidak memiliki izin partners.manage pada entitas ini.",
      },
    },
    catalog: {
      heading: "Katalog Barang & Satuan UoM",
      lead: "Kelola kamus satuan terukur, katalog barang tanpa saldo stok, faktor konversi desimal eksak, dan kelayakan per entitas.",
      backToEntity: "Ringkasan Entitas",
      readOnlyNotice: "Mode baca saja: Anda memiliki izin lihat (catalog.read) tanpa hak ubah (catalog.manage).",
      uomCardTitle: "Kamus Satuan Ukur (UoM)",
      uomCodeLabel: "Kode UoM",
      uomCodePlaceholder: "CTN",
      uomNameLabel: "Nama Satuan",
      uomNamePlaceholder: "Karton Induk",
      uomScaleLabel: "Skala Desimal",
      createUomButton: "Tambah Satuan",
      createUomBusy: "Menyimpan satuan...",
      scaleDiscreteHint: "0 = diskrit utuh (seperti PCS); 1–6 = kontinu (seperti KG)",
      createItemCardTitle: "Daftarkan Barang / Jasa Baru",
      itemCodeLabel: "Kode Barang",
      itemCodePlaceholder: "ITM-KRD-01",
      itemNameLabel: "Nama Barang / Jasa",
      itemNamePlaceholder: "Kardus Induk 60x40x40 cm",
      itemKindLabel: "Jenis Item",
      baseUomLabel: "Satuan Dasar (Base)",
      purchaseUomLabel: "Satuan Beli Default",
      salesUomLabel: "Satuan Jual Default",
      scopeModeLabel: "Cakupan Entitas",
      createItemButton: "Simpan Item Katalog",
      createItemBusy: "Menyimpan item...",
      filterCardTitle: "Cari & Saring Katalog",
      searchLabel: "Cari Kode / Nama Barang",
      searchPlaceholder: "Contoh: ITM-PKG-001 atau Karton",
      kindFilterLabel: "Jenis Item",
      kindFilterAll: "Semua Jenis",
      statusFilterLabel: "Status",
      statusFilterAll: "Semua Status",
      eligibleOnlyLabel: "Hanya diizinkan di entitas ini",
      listCardTitle: "Daftar Barang & Satuan Tenant",
      emptyList: "Tidak ada item katalog yang cocok dengan filter.",
      colItem: "Kode & Nama Item",
      colKind: "Jenis",
      colUoms: "Base · Beli · Jual",
      colEligibility: "Kebijakan Entitas",
      colAction: "Status & Rincian",
      inspectButton: "Kelola UoM & Kebijakan",
      viewItemButton: "Lihat UoM & Kebijakan",
      purchaseEligibleTag: "Beli Diizinkan",
      salesEligibleTag: "Jual Diizinkan",
      ineligibleTag: "Diblokir di Entitas Ini",
      detailCardTitle: "Konversi UoM Eksak & Kebijakan Entitas",
      deactivateButton: "Nonaktifkan Item",
      activateButton: "Aktifkan Kembali",
      toggleScopeButton: "Terapkan Mode Cakupan",
      conversionsTitle: "Aturan Konversi ke Satuan Dasar",
      baseImplicitNote: "Satuan dasar selalu bernilai faktor 1 secara implisit.",
      emptyConversions: "Belum ada satuan alternatif tambahan (menggunakan satuan dasar 1:1).",
      convUomLabel: "Satuan Sumber",
      convFactorLabel: "Faktor ke Satuan Dasar",
      convFactorPlaceholder: "24",
      addConvButton: "Simpan Konversi",
      addConvBusy: "Menyimpan...",
      converterTitle: "Uji Konversi Desimal Eksak (Server Verified)",
      converterUomLabel: "Dari Satuan",
      converterQtyLabel: "Kuantitas Sumber",
      converterQtyPlaceholder: "3",
      convertButton: "Hitung ke Satuan Dasar",
      convertBusy: "Menghitung...",
      convertResultLabel: "Hasil Dasar Eksak",
      convertRoundTripLabel: "Balik ke Satuan Asal",
      policyTitle: "Kebijakan Penggunaan pada Entitas Ini",
      policyPurchaseCheck: "Izinkan Pengadaan (PO) pada entitas ini",
      policySalesCheck: "Izinkan Penjualan (SO) pada entitas ini",
      policyNotesLabel: "Catatan Kebijakan",
      policyNotesPlaceholder: "Contoh: Aktif untuk gudang Jakarta",
      savePolicyButton: "Simpan Kebijakan Entitas",
      savePolicyBusy: "Menyimpan kebijakan...",
      statusLabels: {
        ACTIVE: "Aktif",
        INACTIVE: "Nonaktif",
      },
      kindLabels: {
        STOCKED_GOOD: "Barang Stok",
        NON_STOCK_SERVICE: "Jasa Non-Stok",
      },
      scopeLabels: {
        ALL_ENTITIES: "Semua Entitas",
        RESTRICTED_ENTITIES: "Terbatas per Entitas",
      },
      validation: {
        uomCodeRequired: "Masukkan kode UoM (1–16 karakter).",
        uomNameRequired: "Masukkan nama satuan ukur.",
        itemCodeRequired: "Masukkan kode barang (2–32 karakter).",
        itemNameRequired: "Masukkan nama barang.",
        factorInvalid: "Faktor konversi harus lebih besar dari nol.",
        quantityInvalid: "Masukkan kuantitas numerik lebih dari nol.",
      },
      errors: {
        duplicate_uom_code: "Kode satuan UoM sudah ada dalam tenant ini.",
        duplicate_item_code: "Kode item sudah digunakan dalam tenant ini.",
        base_uom_conversion_not_needed: "Satuan dasar selalu memiliki faktor konversi implisit 1.",
        fractional_discrete_base_quantity: "Kuantitas menghasilkan pecahan untuk satuan dasar diskrit (skala 0).",
        unsupported_uom_conversion: "Tidak ada konversi aktif antara item ini dan satuan tersebut.",
        concurrency_conflict: "Item diubah pengguna lain. Muat ulang terlebih dahulu.",
        insufficient_entity_permission: "Anda tidak memiliki izin catalog.manage pada entitas ini.",
      },
    },
    finance: {
      heading: "Konfigurasi Keuangan & Jurnal Buku Besar",
      lead: "Bagan akun per entitas, periode fiskal, pemetaan subledger, kode pajak terkonfigurasi (OD-07), dan posting jurnal seimbang yang tidak dapat diubah.",
      backToEntity: "Ringkasan Entitas",
      readOnlyNotice: "Mode baca saja: Anda memiliki izin lihat (finance.read) tanpa hak ubah konfigurasi atau posting jurnal.",
      governanceNotice: "Pemberitahuan Kebijakan OD-07 & OD-13: Tarif pajak berstatus UNREVIEWED_CONFIGURABLE dan penomoran periode menunggu persetujuan Product Owner & Konsultan Pajak (ATH-CR-002).",
      tabJournals: "Jurnal & Buku Besar",
      tabCoaMappings: "Bagan Akun & Pemetaan",
      tabPeriodsTax: "Periode Fiskal & Pajak (OD-07)",
      composerTitle: "Buat & Posting Jurnal Seimbang",
      draftBadge: "Draf — Belum Diposting",
      postingDateLabel: "Tanggal Posting",
      sourceKeyLabel: "Kunci Sumber Idempoten",
      sourceKeyPlaceholder: "manual:jrn-2026-11-01",
      memoLabel: "Uraian Jurnal",
      memoPlaceholder: "Biaya operasional kantor Jakarta",
      lineAccountLabel: "Akun Posting",
      lineDescLabel: "Keterangan Baris",
      lineDescPlaceholder: "Detail pos",
      lineDebitLabel: "Debit (IDR)",
      lineCreditLabel: "Kredit (IDR)",
      addLineButton: "Tambah Baris",
      removeLineLabel: "Hapus Baris",
      totalDebitLabel: "Total Debit",
      totalCreditLabel: "Total Kredit",
      deltaLabel: "Selisih",
      balancedTag: "Seimbang (Siap Posting)",
      unbalancedTag: "Tidak Seimbang",
      postJournalButton: "Posting Jurnal Permanen",
      postJournalBusy: "Memposting jurnal...",
      ledgerTitle: "Daftar Jurnal Tercatat (Append-Only)",
      searchPlaceholder: "Cari nomor jurnal, uraian, atau kunci sumber",
      periodFilterAll: "Semua Periode",
      kindFilterAll: "Semua Jenis",
      emptyJournals: "Belum ada jurnal yang cocok dengan saringan.",
      colJournalNumber: "Nomor & Jenis",
      colDatePeriod: "Tanggal & Periode",
      colDescriptionSource: "Uraian & Sumber",
      colDebitCredit: "Debit / Kredit (IDR)",
      colStateAction: "Status & Rincian",
      linesUnitLabel: "baris",
      postedBadge: "Diposting",
      reversedBadge: "Telah Dibalik",
      reversalEntryBadge: "Jurnal Pembalik",
      manageButton: "Rincian & Pembalik",
      viewOnlyButton: "Lihat Baris & Sumber",
      detailTitle: "Rincian Baris & Sumber Jurnal",
      sourceTraceLabel: "Jejak Sumber",
      payloadHashLabel: "Hash Payload",
      viewOriginalJournalButton: "Lihat Jurnal Asal",
      viewReversalJournalButton: "Lihat Jurnal Pembalik",
      colLineAccount: "Akun",
      colLineMemo: "Keterangan",
      colLineDebit: "Debit (IDR)",
      colLineCredit: "Kredit (IDR)",
      reverseSectionTitle: "Pembalik Kompensasi (Append-Only Reversal)",
      reverseDateLabel: "Tanggal Jurnal Pembalik",
      reverseReasonLabel: "Alasan Pembalikan Tertulis",
      reverseReasonPlaceholder: "Koreksi salah akun beban operasional",
      reverseButton: "Posting Jurnal Pembalik",
      reverseBusy: "Memproses pembalikan...",
      coaTitle: "Bagan Akun Entitas (Chart of Accounts)",
      addAccountTitle: "Tambah Akun GL Baru",
      accountCodeLabel: "Kode Akun",
      accountNameLabel: "Nama Akun",
      accountTypeLabel: "Tipe Akun",
      accountCategoryLabel: "Kategori",
      parentAccountLabel: "Akun Induk",
      noParentOption: "Tanpa Induk (Top Level)",
      allowPostingCheck: "Izinkan posting langsung (non-header)",
      controlAccountCheck: "Akun kontrol subledger",
      createAccountButton: "Simpan Akun GL",
      createAccountBusy: "Menyimpan akun...",
      headerGroupTag: "Grup Header",
      postableTag: "Bisa Posting",
      controlTag: "Kontrol",
      deactivateAccountButton: "Nonaktifkan",
      activateAccountButton: "Aktifkan",
      mappingsTitle: "Pemetaan Akun Subledger Wajib (12 Tujuan)",
      colPurpose: "Tujuan Posting",
      colMappedAccount: "Akun Terpetakan",
      colMappingAction: "Aksi",
      saveMappingButton: "Simpan",
      periodsTitle: "Periode Fiskal Entitas",
      addPeriodTitle: "Buka Periode Fiskal Baru",
      periodCodeLabel: "Kode Periode",
      fiscalYearLabel: "Tahun Fiskal",
      periodNumberLabel: "Periode Ke-",
      startDateLabel: "Tanggal Mulai",
      endDateLabel: "Tanggal Selesai",
      createPeriodButton: "Buka Periode Baru",
      createPeriodBusy: "Membuka periode...",
      periodCloseStripTitle: "Kontrol Penutupan Periode",
      dateRangeMobileLabel: "Rentang Tanggal",
      closePeriodNoteLabel: "Catatan Penutupan",
      closePeriodNotePlaceholder: "Tutup buku bulanan",
      closePeriodButton: "Tutup Periode",
      lockPeriodButton: "Kunci Keras",
      taxCodesTitle: "Matriks Kode Pajak Efektif-Tanggal (OD-07)",
      addTaxCodeTitle: "Tambah Kode Pajak Konfigurabel",
      taxCodeLabel: "Kode Pajak",
      taxNameLabel: "Nama Aturan Pajak",
      taxFamilyLabel: "Jenis Pajak",
      taxDirectionLabel: "Arah",
      taxTreatmentLabel: "Perlakuan",
      nominalRateLabel: "Tarif Nominal (%)",
      dppNumeratorLabel: "Pembilang DPP",
      dppDenominatorLabel: "Penyebut DPP",
      effectiveFromLabel: "Berlaku Sejak",
      taxAccountLabel: "Akun GL Pajak",
      noAccountOption: "Tidak tertaut langsung",
      requiresEvidenceCheck: "Wajib bukti dokumen pajak",
      requiresNpwpCheck: "Wajib NPWP lawan transaksi",
      taxNoteLabel: "Catatan Referensi Aturan",
      createTaxButton: "Simpan Kode Pajak",
      createTaxBusy: "Menyimpan pajak...",
      taxPreviewTitle: "Uji Evaluasi Pajak Eksplisit (Server Verified)",
      selectTaxCodeOption: "— Pilih kode pajak secara eksplisit —",
      taxPreviewCodeLabel: "Pilih Kode Pajak (Wajib Eksplisit)",
      taxPreviewAmountLabel: "Nilai Bruto Transaksi (IDR)",
      taxPreviewDateLabel: "Tanggal Transaksi",
      taxPreviewNpwpCheck: "Lawan transaksi memiliki NPWP valid",
      taxPreviewEvidenceCheck: "Bukti dokumen (Faktur / Fasilitas) tersedia",
      taxPreviewButton: "Hitung Dasar & Pajak",
      taxPreviewBusy: "Menghitung...",
      taxPreviewBaseLabel: "Dasar Pengenaan Pajak (DPP)",
      taxPreviewTaxLabel: "Nominal Pajak",
      taxPreviewRateLabel: "Tarif Efektif",
      taxPreviewBlockedLabel: "Pemblokiran Kepatuhan",
      periodStatusLabels: {
        OPEN: "Terbuka",
        CLOSED: "Ditutup",
        LOCKED: "Terkunci",
      },
      accountTypeLabels: {
        ASSET: "Aset",
        LIABILITY: "Liabilitas",
        EQUITY: "Ekuitas",
        REVENUE: "Pendapatan",
        EXPENSE: "Beban",
      },
      normalBalanceLabels: {
        DEBIT: "Debit",
        CREDIT: "Kredit",
      },
      mappingPurposeLabels: {
        INVENTORY_ASSET: "Persediaan (Inventory Asset)",
        GRNI_CLEARING: "Akrual Penerimaan (GRNI)",
        AP_TRADE_CONTROL: "Utang Usaha (AP Control)",
        AR_TRADE_CONTROL: "Piutang Usaha (AR Control)",
        PURCHASE_PRICE_VARIANCE: "Selisih Harga Beli (PPV)",
        PPN_INPUT_TAX: "PPN Masukan",
        PPN_OUTPUT_TAX: "PPN Keluaran",
        PPH_WITHHOLDING_PAYABLE: "Utang Pemotongan PPh",
        BANK_OUTBOUND_CLEARING: "Kliring Bank Keluar",
        BANK_MAIN_OPERATING: "Kas / Bank Utama",
        SALES_REVENUE_DEFAULT: "Pendapatan Penjualan Default",
        COGS_DEFAULT: "HPP / Beban Pokok Default",
      },
      accountCategoryLabels: {
        HEADER_GROUP: "Grup Header (Non-Posting)",
        CASH_BANK: "Kas & Bank",
        BANK_OUTBOUND_CLEARING: "Kliring Bank Keluar",
        ACCOUNTS_RECEIVABLE: "Piutang Usaha",
        INVENTORY_ASSET: "Persediaan",
        TAX_RECEIVABLE: "Pajak Masukan",
        GRNI_ACCRUAL: "Akrual GRNI",
        ACCOUNTS_PAYABLE: "Utang Usaha",
        TAX_PAYABLE: "Utang Pajak Keluaran",
        WITHHOLDING_TAX_PAYABLE: "Utang Pemotongan PPh",
        EQUITY_CAPITAL: "Modal / Ekuitas",
        SALES_REVENUE: "Pendapatan Penjualan",
        COGS_EXPENSE: "Beban Pokok Penjualan (HPP)",
        PURCHASE_PRICE_VARIANCE: "Selisih Harga Beli",
        OPERATING_EXPENSE: "Beban Operasional",
        GENERAL: "Umum",
      },
      taxFamilyLabels: {
        PPN: "PPN (Pajak Pertambahan Nilai)",
        PPH: "PPh (Pajak Penghasilan)",
      },
      taxDirectionLabels: {
        INPUT: "Masukan (Input)",
        OUTPUT: "Keluaran (Output)",
        WITHHOLDING: "Pemotongan (Withholding)",
      },
      taxTreatmentLabels: {
        STANDARD_TAXABLE: "Tarif Standar Kena Pajak",
        SPECIAL_DPP_11_12: "DPP Nilai Lain (11/12)",
        ZERO_RATED: "Tarif 0% / Ekspor",
        EXEMPT: "Dibebaskan PPN",
        NOT_COLLECTED_FACILITY: "Tidak Dipungut (Fasilitas Khusus)",
        WITHHOLDING_DEDUCTION: "Pemotongan PPh",
      },
      taxReviewLabels: {
        UNREVIEWED_CONFIGURABLE: "Template Konfigurabel (Menunggu Review OD-07)",
        REVIEWED_INTERNAL: "Ditinjau Internal",
      },
      validation: {
        memoRequired: "Masukkan uraian jurnal.",
        sourceKeyRequired: "Masukkan kunci sumber idempoten.",
        postingDateRequired: "Pilih tanggal posting.",
        accountCodeRequired: "Masukkan kode akun (2–32 karakter).",
        accountNameRequired: "Masukkan nama akun GL.",
        periodCodeRequired: "Masukkan kode periode (contoh 2026-12).",
        periodYearInvalid: "Tahun fiskal harus antara 2000 dan 2100.",
        periodNumberInvalid: "Nomor periode harus antara 1 dan 13.",
        periodDatesInvalid: "Tanggal selesai harus sama atau setelah tanggal mulai.",
        taxCodeRequired: "Masukkan kode pajak (2–32 karakter).",
        taxNameRequired: "Masukkan nama aturan pajak.",
        taxRateInvalid: "Tarif pajak nominal harus antara 0% dan 100%.",
        taxDppInvalid: "Pembilang DPP harus >= 1 dan <= penyebut DPP.",
        taxEffDateRequired: "Pilih tanggal mulai berlaku kode pajak.",
        taxPreviewAmountInvalid: "Nilai bruto transaksi harus lebih besar dari 0.",
        reversalDateRequired: "Pilih tanggal posting pembalikan.",
        reversalReasonRequired: "Tuliskan alasan pembalikan jurnal.",
      },
      errors: {
        unbalanced_journal: "Jurnal tidak seimbang: Total Debit harus persis sama dengan Total Kredit.",
        invalid_journal_lines: "Minimal 2 baris valid dengan Debit atau Kredit positif satu sisi.",
        unsupported_currency_mvp: "Saat ini posting GL MVP hanya mendukung mata uang fungsional entitas.",
        period_not_found_for_date: "Tidak ada periode fiskal yang mencakup tanggal posting tersebut.",
        period_closed: "Periode fiskal sudah ditutup atau dikunci dan menolak posting baru.",
        duplicate_posting_source_conflict: "Kunci sumber ini sudah digunakan pada jurnal dengan muatan berbeda.",
        account_not_postable: "Akun grup header atau non-posting tidak dapat menerima baris jurnal atau pemetaan.",
        account_inactive: "Akun GL berstatus nonaktif dan tidak boleh digunakan.",
        foreign_or_missing_account: "Akun GL tidak ditemukan dalam entitas hukum ini.",
        mapping_account_type_mismatch: "Tipe akun GL tidak sesuai dengan aturan tujuan pemetaan.",
        gl_account_code_conflict: "Kode akun GL sudah digunakan dalam entitas hukum ini.",
        overlapping_period_dates: "Rentang tanggal periode bertumpuk dengan periode lain.",
        financial_period_conflict: "Kode atau nomor periode fiskal sudah terdaftar.",
        period_not_open: "Periode ini sudah ditutup.",
        journal_already_reversed: "Jurnal ini sudah memiliki jurnal pembalik kompensasi.",
        cannot_reverse_reversal_journal: "Jurnal pembalik tidak dapat dibalik ulang.",
        explicit_tax_code_required: "Kode pajak harus dipilih secara eksplisit.",
        tax_code_not_effective_on_date: "Kode pajak tidak aktif atau di luar tanggal efektif.",
        tax_code_conflict: "Kode pajak dengan tanggal efektif tersebut sudah ada.",
        insufficient_entity_permission: "Anda tidak memiliki izin keuangan yang diperlukan pada entitas ini.",
      },
    },
    procurement: {
      pageTitle: "Pesanan Pembelian (PO), Revisi & Empat Mata",
      backToOverview: "Ringkasan Entitas",
      loadErrorTitle: "Gagal memuat modul Pengadaan & PO",
      retryButton: "Coba Muat Ulang",
      readOnlyBanner: "Mode baca saja: Anda memiliki izin lihat (procurement.read) tanpa hak buat draf, revisi, atau persetujuan empat mata.",
      governanceNotice: "Kebijakan OD-01 & OD-02: Toleransi berlebih terkunci pada 0% (kuantitas tepat) dan setiap pengajuan wajib disetujui oleh pejabat independen.",
      tabListLabel: "Navigasi Pesanan Pembelian",
      tabs: {
        list: "Daftar & Rincian PO",
        editor: "Buat & Edit Draf PO",
      },
      searchPlaceholder: "Cari nomor PO / draf / tujuan...",
      allStatusesOption: "Semua Status PO",
      poListHeading: "Daftar Pesanan Pembelian Entitas",
      emptyList: "Belum ada Pesanan Pembelian yang cocok dengan filter entitas ini.",
      colPoNumber: "Nomor PO / Draf",
      colSupplier: "Pemasok & Tujuan",
      colDates: "Tanggal & Termin",
      colRevision: "Revisi",
      colTotals: "Total & Basis Qty",
      colStatus: "Status & Penerimaan",
      colAction: "Rincian",
      orderDateShort: "Tgl Order",
      expectedDateShort: "Estimasi Tiba",
      revisionPrefix: "Rev",
      openDraftBadge: "Ada revisi terbuka",
      openQtySuffix: "terbuka",
      manageActionButton: "Kelola & Revisi",
      pendingSelfActionButton: "Pantau Status",
      reviewActionButton: "Tinjau & Putuskan",
      readOnlyActionButton: "Lihat Rincian & Riwayat",
      selectPoPrompt: "Pilih baris Pesanan Pembelian di atas untuk melihat snapshot revisi, perbandingan diff perubahan, dan kontrol komitmen.",
      detailHeadingPrefix: "Rincian Pesanan Pembelian",
      snapshotHashLabel: "Hash Snapshot:",
      destinationLabel: "Tujuan Pengiriman:",
      termsLabel: "Termin Pembayaran:",
      termsDaysSuffix: "hari",
      toleranceLabel: "Kebijakan Toleransi (OD-01):",
      createdByLabel: "Dibuat oleh pengguna:",
      submittedByLabel: "Diajukan empat mata oleh:",
      decidedByLabel: "Keputusan persetujuan oleh:",
      changeReasonLabel: "Alasan Perubahan Revisi:",
      notesLabel: "Catatan Komersial:",
      commitmentHeading: "Ringkasan Komitmen Penerimaan (Tanpa Posting Stok S05)",
      orderedBaseLabel: "Total Dipesan (Disetujui)",
      acceptedBaseLabel: "Diterima Bernilai (S07)",
      remainingOpenLabel: "Sisa Komitmen Terbuka",
      receivingProgressLabel: "Progres Penerimaan",
      downstreamRefsLabel: "Referensi Hilir Terhubung:",
      revisionHistoryLabel: "Riwayat Versi Snapshot PO",
      frozenBadge: "Snapshot Beku Permanen",
      editableBadge: "Draf Dapat Diedit",
      lineTableHeading: "Baris Item & Snapshot Pajak",
      colLineNo: "#",
      colItem: "Kode & Nama Item",
      colQtyUom: "Qty & Satuan Order",
      colBaseConversion: "Konversi Unit Dasar",
      colUnitPrice: "Harga Satuan",
      colTax: "Pajak (OD-07)",
      colLineTotal: "Subtotal Baris",
      subtotalLabel: "Subtotal DPP Komersial:",
      ppnTotalLabel: "Total PPN Masukan:",
      whtTotalLabel: "Estimasi PPh Pemotongan:",
      grandTotalLabel: "Total Komitmen Bruto (Grand Total):",
      diffHeading: "Perbandingan Perubahan Revisi",
      diffHeaderTitle: "Perubahan Header Komersial",
      diffLinesTitle: "Perbandingan Baris Item (per Kunci Silsilah)",
      diffTotalDeltaLabel: "Selisih Nilai Bruto:",
      diffBaseQtyDeltaLabel: "Selisih Qty Dasar:",
      diffNoHeaderChanges: "Tidak ada perubahan kolom header komersial dibanding revisi acuan.",
      colDiffLineKey: "Kunci Baris",
      colDiffChange: "Status Ubah",
      colDiffPrevious: "Nilai Acuan Sebelumnya",
      colDiffCurrent: "Nilai Revisi Ini",
      colDiffSubtotal: "Perubahan Subtotal",
      stickyBarHeading: "Panel Kontrol Komitmen & Persetujuan Empat Mata",
      selfApprovalBlockedBanner: "Kontrol Empat Mata (OD-02): Anda adalah pengaju revisi ini sehingga tombol persetujuan dikunci otomatis. Minta pejabat penyetuju independen lain untuk memutuskan.",
      pendingApprovalNote: "Revisi telah dibekukan permanen menunggu keputusan persetujuan empat mata.",
      decisionReasonLabel: "Catatan Keputusan Persetujuan / Penolakan",
      decisionReasonPlaceholder: "Harga & termin diverifikasi",
      approveRevisionButton: "Setujui Revisi PO",
      approveRevisionBusy: "Menyetujui...",
      rejectRevisionButton: "Tolak Revisi",
      rejectRevisionBusy: "Menolak...",
      editDraftButton: "Lanjut Edit Draf",
      submitRevisionButton: "Bekukan & Ajukan Persetujuan",
      submitRevisionBusy: "Mengajukan...",
      amendmentHeading: "Buat Revisi Baru dari PO Disetujui",
      amendmentReasonLabel: "Alasan Revisi / Perubahan Termin",
      amendmentReasonPlaceholder: "Alasan penyesuaian harga / qty",
      createAmendmentButton: "Buat Draf Revisi Baru",
      createAmendmentBusy: "Membuat Revisi...",
      shortCloseHeading: "Tutup Sisa Komitmen Resmi (Short-Close)",
      closeReasonLabel: "Alasan Penutupan Komitmen",
      closeReasonPlaceholder: "Contoh: Sisa barang dibatalkan supplier berdasarkan kesepakatan",
      shortCloseButton: "Tutup Sisa Komitmen (Short-Close)",
      shortCloseBusy: "Menutup...",
      cancelPoHeading: "Batalkan Pesanan Pembelian (Tanpa Penerimaan)",
      cancelReasonLabel: "Alasan Pembatalan PO",
      cancelReasonPlaceholder: "Contoh: Dibatalkan sebelum ada kedatangan gudang",
      cancelPoButton: "Batalkan PO",
      cancelPoBusy: "Membatalkan...",
      cancelBlockedNote: "PO sudah memiliki referensi penerimaan/hilir dan tidak boleh dibatalkan atau dihapus; gunakan Penutupan Sisa Komitmen (Short-Close).",
      terminalPoControlNote: "Komitmen pesanan pembelian ini sudah berada pada status akhir (Ditutup / Dibatalkan); snapshot revisi terkunci permanen dan tidak ada tindakan perubahan lebih lanjut.",
      editorCreateTitle: "Susun Draf Pesanan Pembelian Baru",
      editorEditDraftTitle: "Ubah Draf Revisi Pesanan Pembelian",
      supplierLabel: "Pemasok Layak Entitas",
      selectSupplierOption: "— Pilih Pemasok Aktif —",
      orderDateLabel: "Tanggal Order",
      expectedDateLabel: "Estimasi Tiba",
      destinationInputLabel: "Tujuan Pengiriman (Gudang / Lokasi)",
      destinationPlaceholder: "Gudang Pusat Jakarta",
      termsInputLabel: "Termin Pembayaran (Hari)",
      toleranceSelectLabel: "Kebijakan Toleransi Lebih (OD-01)",
      editorNotesLabel: "Catatan Komersial PO",
      editorNotesPlaceholder: "Instruksi pengiriman, syarat garansi, atau referensi penawaran",
      editorChangeReasonLabel: "Alasan Perubahan Revisi",
      editorChangeReasonPlaceholder: "Ringkas perubahan harga, qty, atau jadwal pengiriman",
      linesSectionHeading: "Daftar Baris Pembelian (Konversi UoM & Pajak Eksplisit)",
      addLineButton: "Tambah Baris Item",
      removeLineButton: "Hapus Baris",
      lineItemLabel: "Item Katalog",
      selectItemOption: "— Pilih Item Layak Beli —",
      lineUomLabel: "Satuan Beli (UoM)",
      lineQtyLabel: "Kuantitas Order",
      linePriceLabel: "Harga Satuan (IDR)",
      lineTaxLabel: "Kode Pajak (OD-07)",
      noTaxOption: "Tanpa Pajak",
      lineDescLabel: "Keterangan Baris",
      lineDescPlaceholder: "Detail pos atau ukuran kemasan",
      linePreviewBasePrefix: "Basis Stok:",
      linePreviewTaxPrefix: "Pajak:",
      od01NoticeLine: "OD-01: Toleransi overdelivery dikunci 0% pada setiap baris.",
      saveDraftOnlyButton: "Simpan sebagai Draf",
      saveAndSubmitButton: "Simpan & Ajukan Persetujuan",
      editorBusy: "Memproses PO...",
      cancelEditorButton: "Kembali ke Daftar PO",
      poStatusLabels: {
        DRAFT: "Draf",
        PENDING_APPROVAL: "Menunggu Persetujuan",
        APPROVED: "Disetujui",
        CLOSED: "Ditutup",
        CANCELLED: "Dibatalkan",
      },
      revisionStatusLabels: {
        DRAFT: "Draf",
        PENDING_APPROVAL: "Menunggu Persetujuan",
        APPROVED: "Disetujui",
        REJECTED: "Ditolak",
        SUPERSEDED: "Digantikan",
        CANCELLED: "Dibatalkan",
      },
      receivingProgressLabels: {
        NOT_APPLICABLE: "Belum Berlaku",
        NOT_RECEIVED: "Belum Diterima",
        PARTIALLY_RECEIVED: "Diterima Sebagian",
        FULLY_RECEIVED: "Lengkap Diterima",
        SHORT_CLOSED: "Ditutup Cepat (Short-Close)",
      },
      diffChangeLabels: {
        ADDED: "Ditambahkan",
        MODIFIED: "Diubah",
        REMOVED: "Dihapus",
        UNCHANGED: "Tetap",
      },
      headerFieldLabels: {
        supplier: "Pemasok",
        orderDate: "Tanggal Order",
        expectedDeliveryDate: "Estimasi Tiba",
        deliveryDestination: "Tujuan Pengiriman",
        paymentTermsDays: "Termin (Hari)",
      },
      itemKindLabels: {
        STOCKED_GOOD: "Barang Persediaan",
        NON_STOCK_SERVICE: "Jasa / Non-Stok",
      },
      validation: {
        supplierRequired: "Pilih pemasok aktif yang layak untuk entitas ini.",
        orderDateRequired: "Tentukan tanggal pesanan pembelian.",
        expectedDateBeforeOrder: "Tanggal estimasi tiba tidak boleh sebelum tanggal order.",
        destinationRequired: "Masukkan gudang atau alamat tujuan pengiriman.",
        paymentTermsInvalid: "Termin pembayaran harus bilangan bulat antara 0 dan 365 hari.",
        linesRequired: "Tambahkan minimal satu baris item pembelian yang valid.",
        lineItemRequired: "Setiap baris harus memilih item katalog.",
        lineQtyInvalid: "Kuantitas pembelian harus lebih besar dari nol.",
        linePriceInvalid: "Harga satuan harus lebih besar dari nol.",
        decisionReasonRequired: "Tuliskan alasan persetujuan atau penolakan empat mata.",
        amendmentReasonRequired: "Tuliskan alasan pembuatan revisi baru.",
        closeReasonRequired: "Tuliskan alasan penutupan sisa komitmen PO.",
        cancelReasonRequired: "Tuliskan alasan pembatalan PO.",
      },
      errors: {
        supplier_not_eligible_for_entity: "Pemasok nonaktif atau dibatasi agar tidak digunakan pada entitas hukum ini.",
        item_not_eligible_for_entity_purchase: "Item tidak aktif atau tidak diizinkan untuk pembelian di entitas ini.",
        unsupported_uom_conversion: "Satuan beli tidak memiliki konversi aktif untuk item yang dipilih.",
        fractional_discrete_base_quantity: "Kuantitas satuan dasar diskrit menghasilkan pecahan desimal yang tidak diizinkan.",
        od01_overdelivery_tolerance_blocked: "OD-01 memblokir toleransi lebih atau overdelivery tanpa batas sebelum disetujui Product Owner.",
        four_eyes_self_approval_forbidden: "Kebijakan empat mata (OD-02) melarang pengaju menyetujui atau memutuskan revisinya sendiri.",
        approval_already_decided: "Permintaan persetujuan revisi PO ini sudah diputuskan sebelumnya.",
        po_revision_frozen_immutable: "Snapshot revisi sudah dibekukan permanen; buat revisi baru untuk mengubah ketentuan.",
        po_has_downstream_references: "PO sudah memiliki referensi hilir dan tidak dapat dibatalkan; gunakan Short-Close.",
        po_has_open_revision_before_close: "Putuskan atau tutup revisi terbuka sebelum menutup sisa komitmen PO.",
        po_open_revision_exists: "Sudah ada revisi draf atau menunggu persetujuan yang masih terbuka pada PO ini.",
        duplicate_po_number: "Nomor PO tersebut sudah terdaftar pada entitas hukum ini.",
        po_delete_forbidden: "Dokumen PO tidak boleh dihapus dari basis data; gunakan Batal atau Short-Close.",
        counterparty_npwp_required_for_rate: "Kode pajak terpilih mewajibkan NPWP pemasok yang valid.",
        tax_code_not_effective_on_date: "Kode pajak tidak aktif pada tanggal order yang dipilih.",
        insufficient_entity_permission: "Anda tidak memiliki izin pengadaan atau persetujuan yang diperlukan di entitas ini.",
      },
    },
    receiving: {
      pageTitle: "Penerimaan Dermaga & Gate 1 (Tanpa Valuasi)",
      backToOverview: "Ringkasan Entitas",
      loadErrorTitle: "Gagal memuat modul Penerimaan Dermaga",
      retryButton: "Muat Ulang Data",
      readOnlyBanner:
        "Mode baca saja: Akun Anda dapat memantau dokumen kedatangan dan riwayat pemeriksaan dermaga tanpa izin pencatatan atau pelepasan.",
      gateOneZeroValuationBanner:
        "Batas Gate 1 Tanpa Valuasi: Kedatangan fisik, penahanan, penolakan, dan pelepasan dermaga tidak mencatat nilai persediaan maupun jurnal GL / GRNI.",
      od01PolicyBanner:
        "Kebijakan OD-01 Aktif: Toleransi lebih kirim terkunci 0%. Kuantitas melebihi komitmen PO yang disetujui ditahan di dermaga dan diblokir dari pelepasan sampai amandemen PO disetujui.",
      tabListLabel: "Navigasi Penerimaan Dermaga",
      tabs: {
        ledger: "Buku Penerimaan Dermaga",
        arrival: "Catat Kedatangan (Gate 1)",
      },
      stats: {
        totalReceipts: "Total GRN",
        deliveredBase: "Diterima Fisik (Dasar)",
        heldBase: "Ditahan OD-01 / Cek",
        releasedBase: "Dirilis ke Gate 2",
        rejectedBase: "Ditolak Tanpa Nilai",
        linkedGlJournals: "Jurnal GL / GRNI Terbentuk",
      },
      filterSearchLabel: "Cari GRN, PO, Surat Jalan, Pemasok",
      filterSearchPlaceholder: "Cari GRN, PO, Surat Jalan...",
      filterStatusLabel: "Filter Status GRN",
      allStatusesOption: "Semua Status",
      refreshButton: "Segarkan",
      registerCardTitle: "Daftar Kedatangan & Kustodi Dermaga (Gate 1)",
      emptyLedgerTitle: "Belum Ada Penerimaan Dermaga",
      emptyLedgerBody:
        "Belum ada dokumen penerimaan fisik pada entitas ini. Catat kedatangan PO yang telah disetujui pada tab Catat Kedatangan (Gate 1).",
      colReceipt: "Nomor GRN & Surat Jalan",
      colPoAndSupplier: "PO Disetujui & Pemasok",
      colStationAndDate: "Dermaga & Tgl Tiba",
      colQuantities: "Ringkasan Kuantitas (Dasar)",
      colStatus: "Status Gate 1",
      colAction: "Tindakan",
      openDetailButton: "Inspeksi",
      activeDetailButton: "Sedang Dibuka",
      arrivalCardTitle: "Catat Kedatangan Fisik di Dermaga (Gate 1)",
      arrivalNoEligiblePosTitle: "Tidak Ada PO Disetujui yang Layak Diterima",
      arrivalNoEligiblePosBody:
        "Penerimaan Gate 1 hanya terbuka bagi PO dengan revisi yang telah disetujui serta memiliki baris barang persediaan.",
      arrivalOpenPoModuleHint: "Pastikan PO telah disetujui pada menu Pengadaan terlebih dahulu.",
      poSelectorLabel: "Pilih PO Disetujui",
      poSelectorPlaceholder: "Pilih PO yang dikirim pemasok...",
      deliveryNoteLabel: "Nomor Surat Jalan Pemasok",
      deliveryNotePlaceholder: "Contoh: SJ-2026-1118-01",
      receivedDateLabel: "Tanggal Kedatangan",
      receivingStationLabel: "Dermaga / Stasiun Penerimaan",
      receivingStationPlaceholder: "Contoh: Dermaga Utama Jakarta · Pintu 2",
      vehicleRefLabel: "Kendaraan / Nomor Segel (Opsional)",
      vehicleRefPlaceholder: "Contoh: B 9104 UX · Segel A-09",
      documentRefLabel: "Referensi Dokumen Surat Jalan (Opsional)",
      documentRefPlaceholder: "Contoh: SCAN-SJ-001.pdf",
      arrivalNotesLabel: "Catatan Kedatangan Dermaga (Opsional)",
      arrivalNotesPlaceholder: "Contoh: Kedatangan parsial gelombang 1, segel baik",
      arrivalLinesHeading: "Baris Barang PO & Kuantitas Tiba di Dermaga",
      colPoLine: "Baris & Item Stok",
      colOrderedRemaining: "Komitmen & Sisa PO",
      colDeliveredQty: "Tiba Fisik & Satuan UoM",
      colArrivalFlags: "Penanda Awal & Alasan",
      deliveredQtyPlaceholder: "0",
      uomSelectLabel: "Satuan UoM",
      flagForInspectionLabel: "Tahan sementara untuk pemeriksaan fisik awal di dermaga",
      discrepancyReasonLabel: "Kode Alasan Selisih / Kondisi",
      discrepancyNotesPlaceholder: "Catatan selisih atau kerusakan kemasan...",
      conditionEvidencePlaceholder: "Ref bukti foto / berita acara...",
      arrivalConvertedBasePrefix: "Konversi satuan dasar",
      arrivalOd01ExcessPreview:
        "Peringatan OD-01: Total tiba melebihi sisa komitmen PO sebesar {excess} {uom}. Bagian berlebih harus ditahan di dermaga atau ditolak tanpa nilai saat pemeriksaan.",
      submitArrivalButton: "Catat Kedatangan Gate 1 (Tanpa Valuasi)",
      submitArrivalBusy: "Menyimpan kedatangan...",
      arrivalSuccessPrefix: "Penerimaan dermaga berhasil dicatat:",
      arrivalReplaySuffix: "(Pemutaran ulang idempoten aman — tidak ada duplikasi)",
      detailCardTitle: "Rincian GRN & Pemeriksaan Kustodi Gate 1",
      selectReceiptPromptTitle: "Pilih Dokumen GRN",
      selectReceiptPromptBody:
        "Pilih baris pada Daftar Kedatangan Dermaga untuk melihat komposisi kuantitas dasar, melakukan pemeriksaan fisik, dan mengaudit riwayat pelepasan ke Gate 2.",
      fieldReceiptNumber: "Nomor Penerimaan (GRN)",
      fieldPoRef: "Referensi PO Terkunci",
      fieldSupplier: "Pemasok Snapshot",
      fieldDeliveryNote: "Nomor Surat Jalan",
      fieldStation: "Dermaga & Tanggal",
      fieldVehicleRef: "Kendaraan / Segel",
      fieldDocumentRef: "Dokumen Bukti",
      fieldFinancialPosting: "Dampak Akuntansi Gate 1",
      noGlPostingBadge: "Tanpa Posting GL / GRNI",
      fieldReceivedBy: "Dicatat Oleh",
      fieldLineCustody: "Kustodi & Konservasi Kuantitas per Baris Barang",
      colCustodyLine: "Baris Barang & UoM",
      colBaseSplit: "Tiba / Menunggu / Ditahan / Dirilis / Ditolak",
      colValuedAccepted: "Diterima Bernilai (Gate 2)",
      colOd01Excess: "Batas & Kelebihan OD-01",
      dockCheckFormHeading: "Perbarui Pemeriksaan & Disposisi Dermaga",
      dockCheckReadOnlyNote:
        "Akun Anda berada dalam mode baca saja sehingga perubahan disposisi kustodi tidak tersedia.",
      conservationFormulaLabel: "Hukum Konservasi (Pending + Held + Released + Rejected)",
      conservationBalancedTag: "Seimbang dengan Fisik Tiba",
      conservationMismatchTag: "Harus Sama dengan Total Tiba",
      od01CapLabel: "Batas Maksimum Rilis PO (OD-01)",
      od01CapExceededTag: "Melebihi batas PO — wajib ditahan atau amandemen PO terlebih dahulu",
      presetReleaseWithinCap: "Lepas Sesuai Batas PO",
      presetHoldAll: "Tahan Semua",
      presetRejectAll: "Tolak Tanpa Nilai",
      pendingQtyLabel: "Menunggu Cek (Pending)",
      heldQtyLabel: "Ditahan Dermaga (Held)",
      releasedQtyLabel: "Dirilis ke Gate 2 (Released)",
      rejectedQtyLabel: "Ditolak Tanpa Nilai (Rejected)",
      inspectionNotesLabel: "Catatan Pemeriksaan Dermaga",
      inspectionNotesPlaceholder: "Contoh: 4 PCS kemasan penyok ditolak; 20 PCS lolos fisik",
      evidenceRefLabel: "Referensi Bukti / Berita Acara (Opsional)",
      evidenceRefPlaceholder: "Contoh: BA-QC-019",
      submitDockCheckButton: "Simpan Hasil Pemeriksaan Gate 1",
      submitDockCheckBusy: "Memperbarui kustodi...",
      dockCheckSuccessBanner: "Status kustodi dermaga dan jejak audit berhasil diperbarui tanpa posting GL.",
      historyCardTitle: "Riwayat Pemeriksaan Dermaga (Append-Only Audit)",
      emptyHistoryBody: "Belum ada catatan pemeriksaan lanjutan setelah kedatangan awal.",
      colCheckedAt: "Waktu Pemeriksaan (WIB)",
      colTransition: "Transisi Status Kustodi",
      colSplitSnapshot: "Snapshot Pembagian (Dasar)",
      colReasonAndActor: "Alasan, Catatan & Pemeriksa",
      receiptStatusLabels: {
        ARRIVED: "Tiba di Dermaga",
        IN_DOCK_INSPECTION: "Dalam Pemeriksaan Dermaga",
        RELEASED_FOR_ACCEPTANCE: "Dirilis ke Gate 2",
        FULLY_REJECTED_UNVALUED: "Ditolak Penuh (Tanpa Nilai)",
      },
      custodyStatusLabels: {
        ARRIVED: "Tiba di Dermaga",
        DOCK_CHECK_PENDING: "Menunggu Cek Fisik",
        DOCK_HELD: "Ditahan di Dermaga",
        DOCK_REJECTED: "Ditolak Tanpa Nilai",
        DOCK_RELEASED: "Dirilis ke Gate 2",
      },
      discrepancyReasonLabels: {
        NONE: "Sesuai / Tanpa Selisih",
        DAMAGED_PACKAGING: "Kemasan Rusak / Penyok",
        WET_OR_DEFECTIVE_GOODS: "Barang Basah / Cacat Fisik",
        OVERDELIVERY_OD01_HOLD: "Kelebihan Kirim (Tahan OD-01)",
        WRONG_ITEM_OR_SPEC: "Barang / Spesifikasi Tidak Sesuai",
        MISSING_SUPPLIER_DOCS: "Dokumen Pemasok Tidak Lengkap",
      },
      defaultReceivingStation: "Dermaga Utama",
      hashPrefix: "Hash:",
      remainingCapPrefix: "Sisa batas",
      capPrefix: "Batas",
      discrepancyNotesLabel: "Catatan Selisih / Kemasan (Opsional)",
      conditionEvidenceLabel: "Ref Bukti / Foto (Opsional)",
      splitCodes: {
        delivered: "Tiba",
        pending: "Cek",
        held: "Tahan",
        released: "Rilis",
        rejected: "Tolak",
      },
      validation: {
        poRequired: "Pilih PO yang telah disetujui sebelum mencatat kedatangan.",
        deliveryNoteRequired: "Masukkan nomor Surat Jalan pemasok.",
        receivedDateRequired: "Pilih tanggal kedatangan barang.",
        stationRequired: "Masukkan dermaga atau stasiun penerimaan.",
        arrivalLinesRequired: "Masukkan kuantitas tiba lebih besar dari nol pada minimal satu baris PO.",
        invalidQtyFormat: "Kuantitas harus berupa angka desimal valid (maksimal 6 angka di belakang koma).",
        conservationMismatch:
          "Jumlah Menunggu + Ditahan + Dirilis + Ditolak harus sama persis dengan kuantitas fisik tiba.",
        discrepancyReasonRequired:
          "Pilih kode alasan selisih atau tuliskan catatan bila terdapat penahanan atau penolakan.",
      },
      errors: {
        idempotency_key_required: "Kunci idempotensi wajib tersedia; coba muat ulang formulir.",
        gr_lines_required: "Pilih minimal satu baris barang PO dengan kuantitas lebih besar dari nol.",
        duplicate_po_line_in_arrival: "Setiap baris PO hanya boleh muncul satu kali per kedatangan GRN.",
        invalid_discrepancy_reason: "Kode alasan selisih tidak valid untuk pemeriksaan dermaga.",
        duplicate_receipt_idempotency_conflict:
          "Kunci idempotensi sudah pernah dipakai dengan isi kedatangan berbeda.",
        duplicate_dock_check_idempotency_conflict:
          "Kunci pemeriksaan sudah pernah dipakai dengan angka disposisi berbeda.",
        foreign_or_missing_purchase_order: "PO tidak ditemukan pada entitas hukum ini.",
        po_not_eligible_for_receiving: "PO tidak memenuhi syarat penerimaan (harus berstatus disetujui dan terbuka).",
        duplicate_supplier_delivery_note:
          "Nomor Surat Jalan tersebut sudah terdaftar untuk pemasok yang sama di entitas ini.",
        po_line_not_in_current_approved_revision:
          "Salah satu baris PO tidak ada pada revisi PO yang sedang disetujui.",
        unsupported_uom_conversion: "Satuan pengiriman tidak memiliki konversi UoM aktif pada item ini.",
        fractional_discrete_base_quantity:
          "Item satuan diskrit tidak mengizinkan kuantitas dasar pecahan.",
        od01_overdelivery_release_blocked:
          "Kebijakan OD-01 memblokir pelepasan melebihi sisa komitmen PO yang disetujui. Tahan kelebihan di dermaga atau lakukan amandemen PO terlebih dahulu.",
        dock_quantity_conservation_mismatch:
          "Hukum konservasi gagal: Menunggu + Ditahan + Dirilis + Ditolak harus sama dengan fisik tiba.",
        dock_release_below_valued_accepted:
          "Kuantitas rilis tidak boleh lebih kecil dari jumlah yang sudah diterima di Gate 2.",
        insufficient_entity_permission:
          "Akun Anda tidak memiliki izin pencatatan atau pemeriksaan penerimaan pada entitas ini.",
        concurrency_conflict: "Rekaman kustodi baru saja diubah pengguna lain; muat ulang sebelum menyimpan.",
      },
    },
  },
  "en-US": {
    appName: "Orphac Athera",
    envLabel: "Verified Foundation · PostgreSQL",
    localeLabel: "Language",
    login: {
      heading: "Sign In to Operations",
      subtitle: "Authenticate using your organization's ASP.NET Core Identity account.",
      emailLabel: "Work Email",
      emailPlaceholder: "name@company.co.id",
      passwordLabel: "Password",
      rememberLabel: "Keep session active on this browser (8h sliding)",
      submitIdle: "Sign in to Athera",
      submitBusy: "Verifying credentials...",
      showPassword: "Show password",
      hidePassword: "Hide password",
      securityNoteTitle: "Session Security Boundary",
      securityPoints: [
        "Credentials are verified exclusively by ASP.NET Core Identity on the API server.",
        "HttpOnly session cookies paired with CSRF validation on all state-changing requests.",
        "Tenant and legal-entity data are isolated per user by PostgreSQL Row-Level Security.",
      ],
      demoHintTitle: "Local Verification Environment (Synthetic Fixture)",
      demoHintBody: "For local development verification, sign in with a seeded @demo.athera.local identity.",
      errors: {
        invalid_credentials: "Incorrect email or password. Check your credentials and try again.",
        account_locked: "Account temporarily locked due to repeated failed attempts. Wait and retry.",
        account_inactive: "This account is inactive. Contact your organization administrator.",
        csrf_validation_failed: "Form security token expired. Please retry.",
        validation_error: "Enter both email and password before signing in.",
        upstream_unavailable: "Backend API service is unreachable. Ensure the API server is running.",
        unknown: "Unable to sign in right now. Please try again.",
      },
    },
    shell: {
      skipToMain: "Skip to main content",
      scopeUnboundBadge: "No Active Scope (Default-Deny)",
      scopeCompactBadge: "No active entity scope",
      scopeUnboundHint: "Contact an Organization Administrator to assign a tenant and entity grants.",
      scopeBoundHint: "Tenant and legal-entity scope are locked transaction-locally via PostgreSQL RLS.",
      selectScope: "Select Entity",
      switchScopeLabel: "Switch legal-entity workspace",
      scopeMenuHeading: "Authorized Legal Entities",
      openDirectoryLink: "Open Workspace Directory",
      activeTag: "Active",
      signOut: "Sign out",
      signingOut: "Signing out...",
      logoutError: "Failed to sign out cleanly. Try again.",
      navTitle: "Operational Modules",
      openMenu: "Open navigation menu",
      closeMenu: "Close navigation menu",
      navItems: {
        overview: "Session Overview",
        workspaces: "Entity Workspaces",
        invitations: "Accept Invitation",
        partners: "Partners",
        catalog: "Catalogue & UoM",
        approvals: "Approvals",
        procurement: "Procurement",
        receiving: "Dock Receiving",
        inventory: "Inventory",
        sales: "Sales",
        finance: "Finance & GL",
        admin: "Administration",
      },
      lockedSuffix: "Not granted in active entity",
      permittedSuffix: "Permitted; opens in subsequent slice",
      modulePlannedHint: "Planned",
      sessionHeading: "Session Identity & Authorization Summary",
      sessionLead: "Account and active workspace assignments loaded live from PostgreSQL under least privilege.",
      recordCardTitle: "PostgreSQL Identity Record",
      fieldUser: "Display Name",
      fieldEmail: "Registered Email",
      fieldLocale: "Preferred Locale",
      fieldLastLogin: "Last Signed In (WIB)",
      fieldWorkspaceStatus: "Workspace Status",
      fieldAuthorizedCount: "Authorized Entities",
      fieldMfaStatus: "TOTP MFA Protection",
      fieldDbVerification: "Database Principal & Connection",
      dbVerifiedValue: "Live PostgreSQL verified (non-superuser runtime role + forced RLS)",
      boundaryTitle: "Multi-Tenant Isolation Boundary",
      boundaryBody:
        "Every scoped operation resolves server-side membership and entity grants before binding transaction-local app.tenant_id in PostgreSQL.",
      nextStepHeading: "Legal-Entity Workspaces",
      nextStepBodyActive:
        "Open the workspace directory to enter an authorized legal entity or switch entities cleanly without stale cache.",
      nextStepBodyEmpty:
        "No active tenant membership or entity grants are assigned. Accept an invitation token or contact an admin.",
      openDirectoryCta: "Open Workspace Directory",
      loadingSession: "Checking active session...",
      errorHeading: "Unable to Verify Session",
      errorRetry: "Retry",
      errorBackToLogin: "Back to Sign In",
    },
    mfa: {
      cardTitle: "TOTP MFA Security (Elevated Approvals)",
      enabledBadge: "MFA Active",
      disabledBadge: "MFA Not Enabled",
      description: "Required before deciding approval cases under high-risk privilege escalation policy.",
      setupButton: "Generate TOTP Secret",
      setupBusy: "Generating secret...",
      sharedKeyLabel: "Authenticator Shared Secret (Base32)",
      codeLabel: "6-Digit Authenticator Code",
      codePlaceholder: "123456",
      verifyButton: "Verify & Enable MFA",
      verifyBusy: "Verifying...",
      verifiedNotice: "TOTP MFA is enabled on your account.",
      codeRequiredError: "Enter the 6-digit code from your authenticator app.",
    },
    invitations: {
      heading: "Accept Organization Invitation",
      lead: "Redeem a single-use invitation token issued by your Organization Administrator for this email.",
      cardTitle: "Legal-Entity Access Activation",
      tokenLabel: "Invitation Token",
      tokenPlaceholder: "Paste invitation token here",
      submitButton: "Accept Invitation & Activate Scope",
      submitBusy: "Validating token...",
      successTitle: "Invitation Accepted",
      successBody: "Your tenant membership and invited entity role grants are active without restoring any prior revoked grants.",
      enterGrantedEntity: "Open Granted Entity",
      tokenRequiredError: "Enter the single-use invitation token provided by your administrator.",
      errors: {
        invitation_not_found: "Invitation token was not found.",
        invitation_expired: "This invitation has expired. Ask an administrator for a new token.",
        invitation_revoked: "This invitation was revoked by an administrator.",
        invitation_already_accepted: "This invitation token has already been redeemed.",
        invitation_email_mismatch: "Your signed-in email does not match the invited recipient.",
      },
    },
    workspaces: {
      heading: "Workspace Directory",
      lead: "Only tenants and legal entities with active membership and grants for your account are listed.",
      tenantCountLabel: "Active tenants",
      entityCountLabel: "Authorized entities",
      colCode: "Entity Code",
      colName: "Legal Entity Name",
      colEntity: "Legal Entity",
      colNpwp: "Tax ID (NPWP)",
      colFiscal: "Currency & Time Zone",
      colFiscalProfile: "Tax ID & Fiscal Context",
      colGrants: "Active Grants",
      colAction: "Action",
      enterWorkspace: "Enter Workspace",
      activeStatus: "Active",
      noNpwp: "Not registered",
      emptyTitle: "No Authorized Workspaces Assigned",
      emptyBody:
        "Your ASP.NET Core Identity account is verified, but you hold zero active tenant memberships or legal-entity access grants. Access remains denied by default.",
      emptyIdentityRef: "Authenticated Identity Reference",
      emptyGuidanceTitle: "Provisioning Workspace Access",
      emptyGuidanceStep1: "Ask an Organization Administrator to issue an entity invitation token for your account email.",
      emptyGuidanceStep2: "Once received, open Accept Invitation to activate scoped entity access.",
      openInvitationCta: "Redeem Invitation Token",
    },
    entitySheet: {
      backToDirectory: "Workspace Directory",
      contextCardTitle: "Legal Entity Profile & Fiscal Context",
      fieldTenant: "Parent Tenant",
      fieldEntityCode: "Entity Code",
      fieldEntityName: "Registered Name",
      fieldNpwp: "Tax ID (NPWP)",
      fieldCurrency: "Functional Currency",
      fieldCountryTz: "Jurisdiction & Time Zone",
      fieldEvaluatedAt: "Server Verification (WIB)",
      permissionsCardTitle: "Effective Permissions & Module Readiness",
      colModule: "Operational Module",
      colPermission: "Permission Code",
      colReadiness: "Readiness State",
      availableState: "Available (S01–S06)",
      permittedPlannedPrefix: "Permitted · Stage",
      notGrantedState: "Not Granted",
      loadingEntity: "Loading isolated entity workspace...",
      unavailableTitle: "Workspace Scope Unavailable",
      unavailableBody:
        "The requested tenant or legal entity does not exist or is outside your active authorization.",
      forbiddenTitle: "Insufficient Entity Permission",
      forbiddenBody:
        "You can access this legal entity, but your active grants do not include the required permission.",
      returnWorkspaces: "Return to Workspace Directory",
      moduleNames: {
        overview: "Entity Overview & Governance",
        partners: "Business Partners (Suppliers & Customers)",
        catalog: "Item Catalogue & UoM Conversions",
        approvals: "Four-Eyes Approval Inbox",
        procurement: "Procurement (PO, GRN, Vendors)",
        receiving: "Dock Custody & Gate 1 Receiving",
        inventory: "Inventory & Stock Ledger",
        sales: "Sales & Invoicing",
        finance: "Finance, Indonesian Tax & GL",
        admin: "Organization Administration & Audit",
      },
    },
    approvals: {
      heading: "Four-Eyes Approval Inbox",
      lead: "Submit scoped entity grant requests or review pending cases independently (OD-02: requester cannot approve own submission).",
      requestCardTitle: "Submit Entity Permission Request",
      policyLabel: "Approval Policy",
      permissionLabel: "Requested Permission",
      reasonLabel: "Business Justification",
      reasonPlaceholder: "e.g., Needed for Q4 stock audit",
      submitButton: "Submit Approval Request",
      submitBusy: "Submitting request...",
      casesCardTitle: "Entity Approval Cases",
      emptyCases: "No approval cases recorded for this legal entity yet.",
      colCase: "Request & Policy",
      colRequester: "Requester & Decider",
      colPolicy: "Policy",
      colStatus: "Status",
      colDecisionAction: "Independent Decision",
      statusPending: "Pending Decision",
      statusApproved: "Approved",
      statusRejected: "Rejected",
      selfApprovalNotice: "Four-Eyes Rule (OD-02): You submitted this request; only another authorized approver can decide it.",
      mfaRequiredTag: "MFA Required",
      reasonRequiredError: "Enter a business justification before submitting this request.",
      decisionReasonPlaceholder: "Written approval or rejection reason...",
      decisionReasonRequired: "Enter a written reason before approving or rejecting.",
      mfaEnrollPrompt: "This policy requires TOTP MFA enrollment before you can decide requests.",
      mfaEnrollLink: "Enable MFA on Account home",
      decidedPrefix: "Decided",
      approveButton: "Approve",
      rejectButton: "Reject",
      decidingBusy: "Deciding...",
      errors: {
        four_eyes_self_approval_forbidden: "Four-eyes rule prohibits deciding your own request.",
        mfa_required: "This policy requires TOTP MFA enrollment before deciding.",
        case_already_decided: "This approval case has already been decided.",
      },
    },
    admin: {
      heading: "Entity Administration, Invitations & Audit",
      lead: "Manage tenant memberships, single-use entity invitations, grants, append-only audit trail, and outbox processing.",
      outboxCardTitle: "Tenant Transactional Outbox Status",
      outboxPending: "Pending",
      outboxProcessed: "Processed",
      outboxFailed: "Failed",
      dispatchOutboxButton: "Process Outbox Queue",
      dispatchBusy: "Dispatching...",
      membersCardTitle: "Tenant Members & Entity Grants",
      colMember: "Member & MFA",
      colMembershipState: "Membership Status",
      colGrants: "Active Entity Grants",
      colMemberActions: "Administration Actions",
      addPermissionLabel: "Add grant",
      grantButton: "Grant",
      suspendButton: "Suspend",
      revokeButton: "Revoke Access",
      mfaActiveTag: "MFA Active",
      mfaOffTag: "No MFA",
      selfLockedHint: "Current actor",
      statusLabels: {
        ACTIVE: "Active",
        SUSPENDED: "Suspended",
        REVOKED: "Revoked",
        PENDING: "Pending",
        ACCEPTED: "Accepted",
        EXPIRED: "Expired",
      },
      inviteCardTitle: "Invite User to This Entity",
      inviteEmailLabel: "Recipient Email",
      inviteEmailPlaceholder: "staff@demo.athera.local",
      inviteEmailRequiredError: "Enter a valid recipient email address.",
      inviteRoleLabel: "Role Template",
      inviteExpiryLabel: "Expiry (Hours)",
      createInviteButton: "Create Invitation",
      createInviteBusy: "Issuing invitation...",
      tokenCreatedTitle: "One-Time Invitation Token Issued",
      tokenCreatedNote: "Copy this token or link now (server persists SHA-256 hash only):",
      copyTokenButton: "Copy Token",
      copiedTokenLabel: "Copied",
      openRedeemLink: "Open invitation redemption page",
      revokeInviteButton: "Revoke Invitation",
      colInviteEmail: "Invited Email",
      colInviteRole: "Role Template",
      colInviteStatus: "Status",
      colInviteExpiry: "Expires (WIB)",
      colInviteAction: "Action",
      auditCardTitle: "Append-Only Entity Audit Trail",
      colAuditTime: "Time (WIB)",
      colAuditEvent: "Event",
      colAuditActor: "Actor",
      colAuditSummary: "Summary",
      emptyAudit: "No audit events recorded yet.",
      errors: {
        invalid_permission_code: "Permission code is not in the approved catalog.",
        grant_already_exists: "Member already holds this active permission.",
      },
    },
    partners: {
      heading: "Business Partners (Suppliers & Customers)",
      lead: "Tenant-shared partner masters with per-legal-entity operational eligibility and metadata-only tax registration.",
      backToEntity: "Entity Summary",
      readOnlyNotice: "Read-only mode: you hold view permission (partners.read) without edit rights (partners.manage).",
      od07MetadataNotice: "OD-07 policy notice: NPWP and PKP status are stored strictly as master metadata; statutory Indonesian PPN/PPh rate determination applies in the Tax module (S04+).",
      filterCardTitle: "Search & Filter Partners",
      searchLabel: "Search Code / Name",
      searchPlaceholder: "e.g., PT-DUAL-01 or Mitra Niaga",
      roleFilterLabel: "Partner Role",
      roleFilterAll: "All Roles",
      roleFilterSupplier: "Suppliers",
      roleFilterCustomer: "Customers",
      statusFilterLabel: "Status",
      statusFilterAll: "All Statuses",
      eligibleOnlyLabel: "Eligible in active entity only",
      createCardTitle: "Register New Business Partner",
      codeLabel: "Partner Code",
      codePlaceholder: "PT-DAGANG-01",
      legalNameLabel: "Legal Entity Name",
      legalNamePlaceholder: "PT Dagang Serba Usaha Nusantara",
      displayNameLabel: "Display Name",
      displayNamePlaceholder: "Dagang Serba Usaha",
      rolesLabel: "Commercial Roles",
      roleSupplierCheck: "Supplier (Purchasing & AP)",
      roleCustomerCheck: "Customer (Sales & AR)",
      npwpLabel: "Tax ID / NPWP (Optional)",
      npwpPlaceholder: "01.333.444.5-011.000",
      pkpLabel: "PKP Metadata",
      scopeModeLabel: "Entity Scope",
      createButton: "Save New Partner",
      createBusy: "Saving partner...",
      listCardTitle: "Tenant Business Partners",
      emptyList: "No partners match the current filter criteria.",
      colCodeName: "Code & Partner Name",
      colRoles: "Roles",
      colScopeTax: "Scope & PKP Metadata",
      colEligibility: "Active Entity Eligibility",
      colStatusAction: "Status & Actions",
      inspectButton: "Inspect & Configure",
      viewDetailsButton: "View Details",
      roleBothBadge: "Supplier & Customer",
      roleSupplierBadge: "Supplier",
      roleCustomerBadge: "Customer",
      supplierEligibleTag: "Purchasing Eligible",
      customerEligibleTag: "Sales Eligible",
      ineligibleTag: "Ineligible Here",
      detailCardTitle: "Partner Detail, Contacts & Entity Eligibility",
      deactivateButton: "Deactivate Partner",
      activateButton: "Reactivate Partner",
      scopeAllOption: "All Active Entities",
      scopeRestrictedOption: "Restricted Entities Only",
      updateScopeLabel: "Entity Scope Policy",
      updateScopeButton: "Apply Scope Mode",
      contactsTitle: "Registered Contacts",
      emptyContacts: "No contacts recorded for this partner yet.",
      contactNameLabel: "Contact Name",
      contactNamePlaceholder: "Dian Permata",
      contactRoleLabel: "Contact Role",
      contactEmailLabel: "Contact Email",
      contactEmailPlaceholder: "dian@partner.co.id",
      contactPhoneLabel: "Phone",
      contactPhonePlaceholder: "+62-21-555-0199",
      contactPrimaryLabel: "Primary contact",
      addContactButton: "Add Contact",
      addContactBusy: "Adding contact...",
      profileTitle: "Current Legal-Entity Eligibility Profile",
      profileSupplierCheck: "Allow as Supplier in this entity",
      profileCustomerCheck: "Allow as Customer in this entity",
      profileTermsLabel: "Payment Terms (Days)",
      profileNotesLabel: "Entity Policy Notes",
      profileNotesPlaceholder: "e.g., Approved vendor for Jakarta hub",
      saveProfileButton: "Save Entity Profile",
      saveProfileBusy: "Saving profile...",
      statusLabels: {
        ACTIVE: "Active",
        INACTIVE: "Inactive",
      },
      scopeLabels: {
        ALL_ENTITIES: "All Entities",
        RESTRICTED_ENTITIES: "Restricted Entities",
      },
      pkpLabels: {
        UNKNOWN: "Unknown",
        PKP: "PKP (Metadata)",
        NON_PKP: "Non-PKP",
      },
      contactRoleLabels: {
        COMMERCIAL: "Commercial / Sales",
        BILLING: "Billing / Tax",
        RECEIVING_DISPATCH: "Receiving / Dispatch",
      },
      validation: {
        codeRequired: "Enter a partner code (2–32 uppercase characters).",
        legalNameRequired: "Enter the partner legal name.",
        roleRequired: "Select at least one role: Supplier or Customer.",
        contactNameRequired: "Enter a contact name before submitting.",
        profileTermsInvalid: "Payment terms must be an integer between 0 and 365 days.",
      },
      errors: {
        duplicate_partner_code: "Partner code already exists in this tenant.",
        partner_role_incompatible: "Entity eligibility cannot enable a role not assigned to this partner.",
        concurrency_conflict: "Partner was modified by another session. Reload before saving.",
        insufficient_entity_permission: "You do not hold partners.manage permission on this entity.",
      },
    },
    catalog: {
      heading: "Item Catalogue & Units of Measure",
      lead: "Maintain tenant units of measure, stock-free item definitions, exact decimal conversions, and per-entity purchase/sales policies.",
      backToEntity: "Entity Summary",
      readOnlyNotice: "Read-only mode: you hold view permission (catalog.read) without edit rights (catalog.manage).",
      uomCardTitle: "Unit of Measure Dictionary",
      uomCodeLabel: "UoM Code",
      uomCodePlaceholder: "CTN",
      uomNameLabel: "Unit Name",
      uomNamePlaceholder: "Master Carton",
      uomScaleLabel: "Decimal Scale",
      createUomButton: "Add UoM",
      createUomBusy: "Saving UoM...",
      scaleDiscreteHint: "0 = discrete integer units (e.g. PCS); 1–6 = continuous decimals (e.g. KG)",
      createItemCardTitle: "Register New Catalogue Item",
      itemCodeLabel: "Item Code",
      itemCodePlaceholder: "ITM-CTN-01",
      itemNameLabel: "Item / Service Name",
      itemNamePlaceholder: "Corrugated Shipping Carton 60x40x40 cm",
      itemKindLabel: "Item Kind",
      baseUomLabel: "Base UoM",
      purchaseUomLabel: "Default Purchase UoM",
      salesUomLabel: "Default Sales UoM",
      scopeModeLabel: "Entity Scope",
      createItemButton: "Save Catalogue Item",
      createItemBusy: "Saving item...",
      filterCardTitle: "Search & Filter Catalogue",
      searchLabel: "Search Code / Item Name",
      searchPlaceholder: "e.g., ITM-PKG-001 or Carton",
      kindFilterLabel: "Item Kind",
      kindFilterAll: "All Kinds",
      statusFilterLabel: "Status",
      statusFilterAll: "All Statuses",
      eligibleOnlyLabel: "Eligible in active entity only",
      listCardTitle: "Tenant Item Catalogue",
      emptyList: "No catalogue items match the current filter criteria.",
      colItem: "Code & Item Name",
      colKind: "Kind",
      colUoms: "Base · Purchase · Sales",
      colEligibility: "Active Entity Policy",
      colAction: "Status & Actions",
      inspectButton: "Configure UoM & Policy",
      viewItemButton: "View UoM & Policy",
      purchaseEligibleTag: "Purchase Allowed",
      salesEligibleTag: "Sales Allowed",
      ineligibleTag: "Blocked Here",
      detailCardTitle: "Exact UoM Conversions & Entity Policy",
      deactivateButton: "Deactivate Item",
      activateButton: "Reactivate Item",
      toggleScopeButton: "Apply Scope Mode",
      conversionsTitle: "Unit Conversions to Base UoM",
      baseImplicitNote: "Base UoM implicitly converts with factor 1.",
      emptyConversions: "No extra unit conversions configured yet (base 1:1 only).",
      convUomLabel: "Source UoM",
      convFactorLabel: "Factor to Base UoM",
      convFactorPlaceholder: "24",
      addConvButton: "Save Conversion",
      addConvBusy: "Saving...",
      converterTitle: "Verify Exact Decimal Conversion (Server-Evaluated)",
      converterUomLabel: "From Unit",
      converterQtyLabel: "Source Quantity",
      converterQtyPlaceholder: "3",
      convertButton: "Convert to Base UoM",
      convertBusy: "Converting...",
      convertResultLabel: "Exact Base Quantity",
      convertRoundTripLabel: "Round-Trip Check",
      policyTitle: "Current Legal-Entity Eligibility Policy",
      policyPurchaseCheck: "Allow Procurement (PO) in this entity",
      policySalesCheck: "Allow Sales (SO) in this entity",
      policyNotesLabel: "Policy Notes",
      policyNotesPlaceholder: "e.g., Active for Jakarta warehouse",
      savePolicyButton: "Save Entity Policy",
      savePolicyBusy: "Saving policy...",
      statusLabels: {
        ACTIVE: "Active",
        INACTIVE: "Inactive",
      },
      kindLabels: {
        STOCKED_GOOD: "Stocked Good",
        NON_STOCK_SERVICE: "Non-Stock Service",
      },
      scopeLabels: {
        ALL_ENTITIES: "All Entities",
        RESTRICTED_ENTITIES: "Restricted Entities",
      },
      validation: {
        uomCodeRequired: "Enter a unit code (1–16 characters).",
        uomNameRequired: "Enter the unit name.",
        itemCodeRequired: "Enter an item code (2–32 characters).",
        itemNameRequired: "Enter the item name.",
        factorInvalid: "Conversion factor must be strictly positive.",
        quantityInvalid: "Enter a positive numeric quantity.",
      },
      errors: {
        duplicate_uom_code: "Unit of measure code already exists in this tenant.",
        duplicate_item_code: "Item code already exists in this tenant.",
        base_uom_conversion_not_needed: "Base unit already has implicit factor 1.",
        fractional_discrete_base_quantity: "Conversion produces a fractional quantity for a discrete scale-0 base UoM.",
        unsupported_uom_conversion: "No active conversion is registered between this item and unit.",
        concurrency_conflict: "Item was modified by another session. Reload before saving.",
        insufficient_entity_permission: "You do not hold catalog.manage permission on this entity.",
      },
    },
    finance: {
      heading: "Financial Configuration & General Ledger",
      lead: "Entity chart of accounts, fiscal periods, subledger account mappings, effective-dated tax codes (OD-07), and immutable balanced journal postings.",
      backToEntity: "Entity Overview",
      readOnlyNotice: "Read-only mode: you hold finance.read without configuration or posting rights.",
      governanceNotice: "OD-07 & OD-13 Policy Notice: Tax rules carry UNREVIEWED_CONFIGURABLE status and statutory numbering/calendar policies await Product Owner & Tax Advisor sign-off (ATH-CR-002).",
      tabJournals: "Journals & Ledger",
      tabCoaMappings: "Chart of Accounts & Mappings",
      tabPeriodsTax: "Periods & Tax Rules (OD-07)",
      composerTitle: "Compose & Post Balanced Journal",
      draftBadge: "Draft — Unposted",
      postingDateLabel: "Posting Date",
      sourceKeyLabel: "Idempotent Source Key",
      sourceKeyPlaceholder: "manual:jrn-2026-11-01",
      memoLabel: "Journal Description",
      memoPlaceholder: "Jakarta office operating expense",
      lineAccountLabel: "Posting Account",
      lineDescLabel: "Line Memo",
      lineDescPlaceholder: "Line detail",
      lineDebitLabel: "Debit (IDR)",
      lineCreditLabel: "Credit (IDR)",
      addLineButton: "Add Line",
      removeLineLabel: "Remove Line",
      totalDebitLabel: "Total Debit",
      totalCreditLabel: "Total Credit",
      deltaLabel: "Variance",
      balancedTag: "Balanced (Ready)",
      unbalancedTag: "Unbalanced",
      postJournalButton: "Commit Balanced Journal",
      postJournalBusy: "Posting journal...",
      ledgerTitle: "Posted Ledger Entries (Append-Only)",
      searchPlaceholder: "Search journal number, description, or source key",
      periodFilterAll: "All Periods",
      kindFilterAll: "All Entry Kinds",
      emptyJournals: "No journal entries match the active filters.",
      colJournalNumber: "Number & Kind",
      colDatePeriod: "Date & Period",
      colDescriptionSource: "Description & Source",
      colDebitCredit: "Debit / Credit (IDR)",
      colStateAction: "Status & Inspect",
      linesUnitLabel: "lines",
      postedBadge: "Posted",
      reversedBadge: "Reversed",
      reversalEntryBadge: "Reversal",
      manageButton: "Inspect & Reverse",
      viewOnlyButton: "View Lines & Source",
      detailTitle: "Journal Lines & Source Traceability",
      sourceTraceLabel: "Posting Source",
      payloadHashLabel: "Payload Hash",
      viewOriginalJournalButton: "View Original Journal",
      viewReversalJournalButton: "View Reversal Journal",
      colLineAccount: "Account",
      colLineMemo: "Line Memo",
      colLineDebit: "Debit (IDR)",
      colLineCredit: "Credit (IDR)",
      reverseSectionTitle: "Compensating Reversal (Append-Only)",
      reverseDateLabel: "Reversal Posting Date",
      reverseReasonLabel: "Written Reversal Reason",
      reverseReasonPlaceholder: "Correction for misclassified expense",
      reverseButton: "Post Reversal Journal",
      reverseBusy: "Posting reversal...",
      coaTitle: "Entity Chart of Accounts",
      addAccountTitle: "Create New GL Account",
      accountCodeLabel: "Account Code",
      accountNameLabel: "Account Name",
      accountTypeLabel: "Account Type",
      accountCategoryLabel: "Category",
      parentAccountLabel: "Parent Account",
      noParentOption: "No Parent (Top Level)",
      allowPostingCheck: "Allow direct ledger postings (non-header)",
      controlAccountCheck: "Subledger control account",
      createAccountButton: "Save GL Account",
      createAccountBusy: "Saving account...",
      headerGroupTag: "Header Group",
      postableTag: "Postable",
      controlTag: "Control",
      deactivateAccountButton: "Deactivate",
      activateAccountButton: "Activate",
      mappingsTitle: "Mandatory Subledger Account Mappings (12 Purposes)",
      colPurpose: "Posting Purpose",
      colMappedAccount: "Mapped Account",
      colMappingAction: "Action",
      saveMappingButton: "Save",
      periodsTitle: "Entity Fiscal Periods",
      addPeriodTitle: "Open New Fiscal Period",
      periodCodeLabel: "Period Code",
      fiscalYearLabel: "Fiscal Year",
      periodNumberLabel: "Period #",
      startDateLabel: "Start Date",
      endDateLabel: "End Date",
      createPeriodButton: "Open Period",
      createPeriodBusy: "Opening period...",
      periodCloseStripTitle: "Period Closing Controls",
      dateRangeMobileLabel: "Date Window",
      closePeriodNoteLabel: "Close Note",
      closePeriodNotePlaceholder: "Monthly close",
      closePeriodButton: "Close Period",
      lockPeriodButton: "Hard Lock",
      taxCodesTitle: "Effective-Dated Tax Code Matrix (OD-07)",
      addTaxCodeTitle: "Create Configurable Tax Code",
      taxCodeLabel: "Tax Code",
      taxNameLabel: "Tax Rule Name",
      taxFamilyLabel: "Tax Family",
      taxDirectionLabel: "Direction",
      taxTreatmentLabel: "Treatment",
      nominalRateLabel: "Nominal Rate (%)",
      dppNumeratorLabel: "DPP Numerator",
      dppDenominatorLabel: "DPP Denominator",
      effectiveFromLabel: "Effective From",
      taxAccountLabel: "GL Tax Account",
      noAccountOption: "Not directly linked",
      requiresEvidenceCheck: "Requires documentary evidence",
      requiresNpwpCheck: "Requires counterparty NPWP",
      taxNoteLabel: "Reference Note",
      createTaxButton: "Save Tax Code",
      createTaxBusy: "Saving tax code...",
      taxPreviewTitle: "Explicit Tax Determination Preview (Server Verified)",
      selectTaxCodeOption: "— Select explicitly —",
      taxPreviewCodeLabel: "Select Explicit Tax Code",
      taxPreviewAmountLabel: "Gross Transaction Amount (IDR)",
      taxPreviewDateLabel: "Transaction Date",
      taxPreviewNpwpCheck: "Counterparty has valid NPWP",
      taxPreviewEvidenceCheck: "Documentary evidence (Faktur / FTZ) provided",
      taxPreviewButton: "Evaluate Tax & Base",
      taxPreviewBusy: "Evaluating...",
      taxPreviewBaseLabel: "Taxable Base (DPP)",
      taxPreviewTaxLabel: "Computed Tax Amount",
      taxPreviewRateLabel: "Effective Rate",
      taxPreviewBlockedLabel: "Compliance Gate",
      periodStatusLabels: {
        OPEN: "Open",
        CLOSED: "Closed",
        LOCKED: "Locked",
      },
      accountTypeLabels: {
        ASSET: "Asset",
        LIABILITY: "Liability",
        EQUITY: "Equity",
        REVENUE: "Revenue",
        EXPENSE: "Expense",
      },
      normalBalanceLabels: {
        DEBIT: "Debit",
        CREDIT: "Credit",
      },
      mappingPurposeLabels: {
        INVENTORY_ASSET: "Inventory Asset",
        GRNI_CLEARING: "GRNI Accrual Clearing",
        AP_TRADE_CONTROL: "Accounts Payable Control",
        AR_TRADE_CONTROL: "Accounts Receivable Control",
        PURCHASE_PRICE_VARIANCE: "Purchase Price Variance",
        PPN_INPUT_TAX: "PPN Input Tax Receivable",
        PPN_OUTPUT_TAX: "PPN Output Tax Payable",
        PPH_WITHHOLDING_PAYABLE: "PPh Withholding Payable",
        BANK_OUTBOUND_CLEARING: "Bank Outbound Clearing",
        BANK_MAIN_OPERATING: "Main Operating Cash / Bank",
        SALES_REVENUE_DEFAULT: "Default Sales Revenue",
        COGS_DEFAULT: "Default Cost of Goods Sold",
      },
      accountCategoryLabels: {
        HEADER_GROUP: "Header Group (Non-Posting)",
        CASH_BANK: "Cash & Bank",
        BANK_OUTBOUND_CLEARING: "Bank Outbound Clearing",
        ACCOUNTS_RECEIVABLE: "Accounts Receivable",
        INVENTORY_ASSET: "Inventory Asset",
        TAX_RECEIVABLE: "Input Tax Receivable",
        GRNI_ACCRUAL: "GRNI Accrual",
        ACCOUNTS_PAYABLE: "Accounts Payable",
        TAX_PAYABLE: "Output Tax Payable",
        WITHHOLDING_TAX_PAYABLE: "Withholding Tax Payable",
        EQUITY_CAPITAL: "Equity Capital",
        SALES_REVENUE: "Sales Revenue",
        COGS_EXPENSE: "Cost of Goods Sold",
        PURCHASE_PRICE_VARIANCE: "Purchase Price Variance",
        OPERATING_EXPENSE: "Operating Expense",
        GENERAL: "General",
      },
      taxFamilyLabels: {
        PPN: "PPN (Value-Added Tax)",
        PPH: "PPh (Withholding Income Tax)",
      },
      taxDirectionLabels: {
        INPUT: "Input Tax",
        OUTPUT: "Output Tax",
        WITHHOLDING: "Withholding",
      },
      taxTreatmentLabels: {
        STANDARD_TAXABLE: "Standard Taxable Rate",
        SPECIAL_DPP_11_12: "Special Taxable Base (11/12 DPP)",
        ZERO_RATED: "Zero-Rated / Export",
        EXEMPT: "Exempt",
        NOT_COLLECTED_FACILITY: "Not Collected (Special Facility)",
        WITHHOLDING_DEDUCTION: "Withholding Deduction",
      },
      taxReviewLabels: {
        UNREVIEWED_CONFIGURABLE: "Configurable Template (Awaiting OD-07 Sign-Off)",
        REVIEWED_INTERNAL: "Internally Reviewed",
      },
      validation: {
        memoRequired: "Enter a journal description.",
        sourceKeyRequired: "Enter an idempotent source key.",
        postingDateRequired: "Choose a posting date.",
        accountCodeRequired: "Enter a GL account code (2–32 characters).",
        accountNameRequired: "Enter a GL account name.",
        periodCodeRequired: "Enter a period code (e.g., 2026-12).",
        periodYearInvalid: "Fiscal year must be between 2000 and 2100.",
        periodNumberInvalid: "Period number must be between 1 and 13.",
        periodDatesInvalid: "End date must be on or after start date.",
        taxCodeRequired: "Enter a tax code (2–32 characters).",
        taxNameRequired: "Enter a tax rule name.",
        taxRateInvalid: "Nominal tax rate must be between 0% and 100%.",
        taxDppInvalid: "DPP numerator must be >= 1 and <= DPP denominator.",
        taxEffDateRequired: "Choose the effective start date.",
        taxPreviewAmountInvalid: "Gross transaction amount must exceed zero.",
        reversalDateRequired: "Choose a reversal posting date.",
        reversalReasonRequired: "Enter a written reversal reason.",
      },
      errors: {
        unbalanced_journal: "Journal is unbalanced: Total Debit must equal Total Credit.",
        invalid_journal_lines: "At least 2 valid single-sided lines with positive amounts are required.",
        unsupported_currency_mvp: "Ledger postings currently require the entity's functional currency.",
        period_not_found_for_date: "No fiscal period is configured covering this posting date.",
        period_closed: "This fiscal period is closed or locked and rejects new postings.",
        duplicate_posting_source_conflict: "This idempotent source key already exists with a different payload.",
        account_not_postable: "Header or non-postable accounts cannot receive journal lines or mappings.",
        account_inactive: "Inactive GL accounts cannot be used for posting.",
        foreign_or_missing_account: "GL account was not found in this legal entity.",
        mapping_account_type_mismatch: "Account type does not satisfy the selected mapping purpose.",
        gl_account_code_conflict: "GL account code already exists in this legal entity.",
        overlapping_period_dates: "Date range overlaps with an existing fiscal period.",
        financial_period_conflict: "Fiscal period code or year/number already exists.",
        period_not_open: "This fiscal period is already closed.",
        journal_already_reversed: "This journal entry has already been reversed.",
        cannot_reverse_reversal_journal: "A reversal journal cannot itself be reversed.",
        explicit_tax_code_required: "An explicit effective-dated tax code must be selected.",
        tax_code_not_effective_on_date: "Selected tax code is inactive or outside its effective window.",
        tax_code_conflict: "A tax code with the same code and effective date already exists.",
        insufficient_entity_permission: "You do not hold required finance permissions on this entity.",
      },
    },
    procurement: {
      pageTitle: "Purchase Orders (PO), Revisions & Four-Eyes",
      backToOverview: "Entity Overview",
      loadErrorTitle: "Failed to load Procurement & PO workspace",
      retryButton: "Reload Data",
      readOnlyBanner: "Read-only mode: You hold procurement.read without draft creation, revision, or four-eyes approval rights.",
      governanceNotice: "OD-01 & OD-02 Policy: Overdelivery tolerance is locked at 0% (exact quantity) and every commitment requires independent four-eyes approval.",
      tabListLabel: "Purchase Order Navigation",
      tabs: {
        list: "PO Register & Detail",
        editor: "Create & Edit PO Draft",
      },
      searchPlaceholder: "Search PO, draft, or destination...",
      allStatusesOption: "All PO Statuses",
      poListHeading: "Entity Purchase Orders Register",
      emptyList: "No Purchase Orders match the current filter in this entity.",
      colPoNumber: "PO / Draft Number",
      colSupplier: "Supplier & Destination",
      colDates: "Dates & Terms",
      colRevision: "Revision",
      colTotals: "Total & Base Qty",
      colStatus: "Status & Receiving",
      colAction: "Inspect",
      orderDateShort: "Order Date",
      expectedDateShort: "Expected",
      revisionPrefix: "Rev",
      openDraftBadge: "Open revision",
      openQtySuffix: "open",
      manageActionButton: "Manage & Revise",
      pendingSelfActionButton: "View Status",
      reviewActionButton: "Review & Decide",
      readOnlyActionButton: "Inspect Lines & History",
      selectPoPrompt: "Select a Purchase Order row above to inspect immutable revision snapshots, comparison diffs, and commitment controls.",
      detailHeadingPrefix: "Purchase Order Detail",
      snapshotHashLabel: "Snapshot Hash:",
      destinationLabel: "Delivery Destination:",
      termsLabel: "Payment Terms:",
      termsDaysSuffix: "days",
      toleranceLabel: "Tolerance Policy (OD-01):",
      createdByLabel: "Created by user:",
      submittedByLabel: "Submitted for four-eyes by:",
      decidedByLabel: "Decision recorded by:",
      changeReasonLabel: "Revision Change Reason:",
      notesLabel: "Commercial Notes:",
      commitmentHeading: "Receiving Commitment Summary (Zero Stock Impact in S05)",
      orderedBaseLabel: "Approved Ordered Base Qty",
      acceptedBaseLabel: "Valued Accepted (S07)",
      remainingOpenLabel: "Remaining Open Commitment",
      receivingProgressLabel: "Receiving Progress",
      downstreamRefsLabel: "Downstream References:",
      revisionHistoryLabel: "PO Snapshot Revisions",
      frozenBadge: "Frozen Snapshot",
      editableBadge: "Editable Draft",
      lineTableHeading: "Line Items & Tax Snapshots",
      colLineNo: "#",
      colItem: "Item Code & Name",
      colQtyUom: "Order Qty & UoM",
      colBaseConversion: "Base Unit Conversion",
      colUnitPrice: "Unit Price",
      colTax: "Tax (OD-07)",
      colLineTotal: "Line Subtotal",
      subtotalLabel: "Commercial Subtotal:",
      ppnTotalLabel: "Input VAT (PPN) Total:",
      whtTotalLabel: "Estimated Withholding Tax:",
      grandTotalLabel: "Gross Commitment Total:",
      diffHeading: "Approval Comparison Diff",
      diffHeaderTitle: "Commercial Header Changes",
      diffLinesTitle: "Line Item Changes (Matched by Lineage Key)",
      diffTotalDeltaLabel: "Grand Total Delta:",
      diffBaseQtyDeltaLabel: "Base Qty Delta:",
      diffNoHeaderChanges: "No commercial header field changes versus baseline revision.",
      colDiffLineKey: "Line Key",
      colDiffChange: "Change",
      colDiffPrevious: "Previous Baseline",
      colDiffCurrent: "Current Revision",
      colDiffSubtotal: "Subtotal Change",
      stickyBarHeading: "Four-Eyes Commitment & Lifecycle Controls",
      selfApprovalBlockedBanner: "Four-Eyes Control (OD-02): You submitted this revision, so approval controls are locked. Ask an independent authorized approver to decide.",
      pendingApprovalNote: "This revision snapshot is permanently frozen pending independent four-eyes review.",
      decisionReasonLabel: "Four-Eyes Decision Reason",
      decisionReasonPlaceholder: "Pricing & terms verified",
      approveRevisionButton: "Approve PO Revision",
      approveRevisionBusy: "Approving...",
      rejectRevisionButton: "Reject Revision",
      rejectRevisionBusy: "Rejecting...",
      editDraftButton: "Resume Draft Edit",
      submitRevisionButton: "Freeze & Submit for Approval",
      submitRevisionBusy: "Submitting...",
      amendmentHeading: "Create Amendment Revision from Approved PO",
      amendmentReasonLabel: "Amendment / Revision Reason",
      amendmentReasonPlaceholder: "Reason for price / qty update",
      createAmendmentButton: "Create Draft Amendment",
      createAmendmentBusy: "Creating Revision...",
      shortCloseHeading: "Authorize Remaining Commitment Close (Short-Close)",
      closeReasonLabel: "Short-Close Justification",
      closeReasonPlaceholder: "e.g., Supplier discontinued remaining units by mutual agreement",
      shortCloseButton: "Short-Close Remaining Commitment",
      shortCloseBusy: "Closing...",
      cancelPoHeading: "Cancel Purchase Order (No Downstream Receipts)",
      cancelReasonLabel: "Cancellation Reason",
      cancelReasonPlaceholder: "e.g., Cancelled prior to supplier dispatch",
      cancelPoButton: "Cancel PO",
      cancelPoBusy: "Cancelling...",
      cancelBlockedNote: "Downstream receiving or billing references exist, so cancellation/deletion is blocked; use authorized Short-Close instead.",
      terminalPoControlNote: "This purchase order commitment is in a terminal state (Closed / Cancelled); revision snapshots remain permanently locked with no further mutation actions.",
      editorCreateTitle: "Draft New Purchase Order",
      editorEditDraftTitle: "Edit Draft Purchase Order Revision",
      supplierLabel: "Entity-Eligible Supplier",
      selectSupplierOption: "— Select Active Supplier —",
      orderDateLabel: "Order Date",
      expectedDateLabel: "Expected Delivery",
      destinationInputLabel: "Delivery Destination",
      destinationPlaceholder: "Jakarta Central Warehouse",
      termsInputLabel: "Payment Terms (Days)",
      toleranceSelectLabel: "Overdelivery Tolerance Policy (OD-01)",
      editorNotesLabel: "PO Commercial Notes",
      editorNotesPlaceholder: "Delivery instructions, warranty terms, or quotation ref",
      editorChangeReasonLabel: "Revision Change Reason",
      editorChangeReasonPlaceholder: "Summarize pricing, quantity, or schedule updates",
      linesSectionHeading: "Purchase Lines (UoM Conversion & Explicit Tax)",
      addLineButton: "Add Line Item",
      removeLineButton: "Remove Line",
      lineItemLabel: "Catalogue Item",
      selectItemOption: "— Select Purchase-Eligible Item —",
      lineUomLabel: "Order UoM",
      lineQtyLabel: "Order Qty",
      linePriceLabel: "Unit Price (IDR)",
      lineTaxLabel: "Tax Code (OD-07)",
      noTaxOption: "No Tax",
      lineDescLabel: "Line Description",
      lineDescPlaceholder: "Line notes or packaging details",
      linePreviewBasePrefix: "Base Qty:",
      linePreviewTaxPrefix: "Tax:",
      od01NoticeLine: "OD-01: Overdelivery tolerance locked at 0% on all lines.",
      saveDraftOnlyButton: "Save Draft",
      saveAndSubmitButton: "Save & Submit for Approval",
      editorBusy: "Saving PO...",
      cancelEditorButton: "Back to PO List",
      poStatusLabels: {
        DRAFT: "Draft",
        PENDING_APPROVAL: "Pending Approval",
        APPROVED: "Approved",
        CLOSED: "Closed",
        CANCELLED: "Cancelled",
      },
      revisionStatusLabels: {
        DRAFT: "Draft",
        PENDING_APPROVAL: "Pending Approval",
        APPROVED: "Approved",
        REJECTED: "Rejected",
        SUPERSEDED: "Superseded",
        CANCELLED: "Cancelled",
      },
      receivingProgressLabels: {
        NOT_APPLICABLE: "Not Applicable",
        NOT_RECEIVED: "Not Received",
        PARTIALLY_RECEIVED: "Partially Received",
        FULLY_RECEIVED: "Fully Received",
        SHORT_CLOSED: "Short-Closed",
      },
      diffChangeLabels: {
        ADDED: "Added",
        MODIFIED: "Modified",
        REMOVED: "Removed",
        UNCHANGED: "Unchanged",
      },
      headerFieldLabels: {
        supplier: "Supplier",
        orderDate: "Order Date",
        expectedDeliveryDate: "Expected Delivery",
        deliveryDestination: "Delivery Destination",
        paymentTermsDays: "Payment Terms (Days)",
      },
      itemKindLabels: {
        STOCKED_GOOD: "Stocked Good",
        NON_STOCK_SERVICE: "Service / Non-Stock",
      },
      validation: {
        supplierRequired: "Select an eligible active supplier for this legal entity.",
        orderDateRequired: "Choose an order date.",
        expectedDateBeforeOrder: "Expected delivery date cannot precede the order date.",
        destinationRequired: "Enter a delivery warehouse or destination.",
        paymentTermsInvalid: "Payment terms must be an integer between 0 and 365 days.",
        linesRequired: "Add at least one valid purchase order line item.",
        lineItemRequired: "Each line requires selecting an eligible catalogue item.",
        lineQtyInvalid: "Order quantity must be greater than zero.",
        linePriceInvalid: "Unit price must be greater than zero.",
        decisionReasonRequired: "Provide a written four-eyes approval or rejection reason.",
        amendmentReasonRequired: "Enter a written reason for creating the amendment revision.",
        closeReasonRequired: "Enter a written justification for short-closing the PO.",
        cancelReasonRequired: "Enter a written reason for cancelling this PO.",
      },
      errors: {
        supplier_not_eligible_for_entity: "Selected partner is inactive or restricted from supplying this legal entity.",
        item_not_eligible_for_entity_purchase: "Selected item is inactive or restricted from purchasing in this entity.",
        unsupported_uom_conversion: "No active UoM conversion factor exists for the selected item and unit.",
        fractional_discrete_base_quantity: "Discrete base quantity cannot evaluate to a fractional decimal.",
        od01_overdelivery_tolerance_blocked: "OD-01 blocks non-zero overdelivery tolerance or unlimited flags until Product Owner sign-off.",
        four_eyes_self_approval_forbidden: "Four-eyes policy (OD-02) prohibits approving or deciding your own submitted PO revision.",
        approval_already_decided: "This purchase order approval request has already been decided.",
        po_revision_frozen_immutable: "This revision snapshot is permanently frozen; create a new revision instead.",
        po_has_downstream_references: "PO already has downstream references and cannot be cancelled; use Short-Close.",
        po_has_open_revision_before_close: "Decide or cancel open draft/pending revisions before short-closing.",
        po_open_revision_exists: "An open draft or pending approval revision already exists for this PO.",
        duplicate_po_number: "This PO number already exists inside this legal entity.",
        po_delete_forbidden: "Purchase orders are immutable and cannot be deleted; use Cancel or Short-Close.",
        counterparty_npwp_required_for_rate: "Selected tax rule requires a valid supplier NPWP.",
        tax_code_not_effective_on_date: "Selected tax code is inactive on this PO order date.",
        insufficient_entity_permission: "Your active grants do not permit this procurement or approval operation.",
      },
    },
    receiving: {
      pageTitle: "Dock Custody & Gate 1 Receiving (Unvalued)",
      backToOverview: "Entity Overview",
      loadErrorTitle: "Failed to load Dock Receiving workspace",
      retryButton: "Reload Data",
      readOnlyBanner:
        "Read-only mode: Your role can monitor dock arrivals and inspection history without recording receipts or releasing custody.",
      gateOneZeroValuationBanner:
        "Gate 1 Zero-Valuation Boundary: Physical arrivals, dock holds, unvalued rejections, and dock releases create zero inventory valuation or GL / GRNI postings.",
      od01PolicyBanner:
        "OD-01 Policy Enforced: Overdelivery tolerance is locked at 0%. Quantities exceeding approved PO commitments stay held at the dock and blocked from release until a PO amendment is approved.",
      tabListLabel: "Dock Receiving Navigation",
      tabs: {
        ledger: "Dock Receipt Register",
        arrival: "Record Dock Arrival (Gate 1)",
      },
      stats: {
        totalReceipts: "Total GRNs",
        deliveredBase: "Delivered Physical (Base)",
        heldBase: "Held OD-01 / Pending",
        releasedBase: "Released to Gate 2",
        rejectedBase: "Rejected Unvalued",
        linkedGlJournals: "GL / GRNI Journals Posted",
      },
      filterSearchLabel: "Search GRN, PO, Delivery Note, Supplier",
      filterSearchPlaceholder: "Search GRN, PO, Delivery Note...",
      filterStatusLabel: "Filter GRN Status",
      allStatusesOption: "All Statuses",
      refreshButton: "Refresh",
      registerCardTitle: "Dock Arrivals & Gate 1 Custody Register",
      emptyLedgerTitle: "No Dock Receipts Recorded",
      emptyLedgerBody:
        "No physical dock arrivals exist in this legal entity yet. Record a supplier delivery against an approved PO using Record Dock Arrival (Gate 1).",
      colReceipt: "GRN & Delivery Note",
      colPoAndSupplier: "Approved PO & Supplier",
      colStationAndDate: "Station & Arrival Date",
      colQuantities: "Quantity Split (Base)",
      colStatus: "Gate 1 Status",
      colAction: "Action",
      openDetailButton: "Inspect",
      activeDetailButton: "Inspecting",
      arrivalCardTitle: "Record Physical Arrival at Receiving Dock (Gate 1)",
      arrivalNoEligiblePosTitle: "No Approved POs Eligible for Receiving",
      arrivalNoEligiblePosBody:
        "Gate 1 receiving is open only for purchase orders with an approved open revision containing stocked goods.",
      arrivalOpenPoModuleHint: "Approve the purchase order revision in Procurement before recording arrival.",
      poSelectorLabel: "Select Approved Purchase Order",
      poSelectorPlaceholder: "Choose delivered PO...",
      deliveryNoteLabel: "Supplier Delivery Note (Surat Jalan)",
      deliveryNotePlaceholder: "e.g. SJ-2026-1118-01",
      receivedDateLabel: "Arrival Date",
      receivingStationLabel: "Receiving Dock / Station",
      receivingStationPlaceholder: "e.g. Jakarta Main Dock · Bay 2",
      vehicleRefLabel: "Vehicle / Seal Ref (Optional)",
      vehicleRefPlaceholder: "e.g. B 9104 UX · Seal A-09",
      documentRefLabel: "Delivery Note Document Ref (Optional)",
      documentRefPlaceholder: "e.g. SCAN-SJ-001.pdf",
      arrivalNotesLabel: "Dock Arrival Notes (Optional)",
      arrivalNotesPlaceholder: "e.g. Partial batch 1 delivery, carrier seal intact",
      arrivalLinesHeading: "PO Lines & Physically Delivered Quantities",
      colPoLine: "Line & Stock Item",
      colOrderedRemaining: "Ordered & Remaining Cap",
      colDeliveredQty: "Delivered Qty & UoM",
      colArrivalFlags: "Initial Flag & Reason",
      deliveredQtyPlaceholder: "0",
      uomSelectLabel: "UoM",
      flagForInspectionLabel: "Hold initially for dock inspection",
      discrepancyReasonLabel: "Discrepancy / Condition Code",
      discrepancyNotesPlaceholder: "Discrepancy or packaging damage notes...",
      conditionEvidencePlaceholder: "Photo or inspection memo reference...",
      arrivalConvertedBasePrefix: "Converted base quantity",
      arrivalOd01ExcessPreview:
        "OD-01 Warning: Delivered quantity exceeds remaining PO commitment by {excess} {uom}. Excess must remain held at dock or be rejected unvalued upon inspection.",
      submitArrivalButton: "Record Gate 1 Arrival (Unvalued)",
      submitArrivalBusy: "Recording arrival...",
      arrivalSuccessPrefix: "Dock arrival recorded:",
      arrivalReplaySuffix: "(Idempotent replay confirmed — zero duplicates created)",
      detailCardTitle: "GRN Detail & Gate 1 Custody Inspection",
      selectReceiptPromptTitle: "Select a Dock Receipt",
      selectReceiptPromptBody:
        "Select a row in the Dock Receipt Register to inspect base-quantity conservation, adjust custody splits, and audit append-only Gate 1 history.",
      fieldReceiptNumber: "Receipt Number (GRN)",
      fieldPoRef: "Frozen PO Reference",
      fieldSupplier: "Supplier Snapshot",
      fieldDeliveryNote: "Delivery Note (SJ)",
      fieldStation: "Dock & Arrival Date",
      fieldVehicleRef: "Vehicle / Seal",
      fieldDocumentRef: "Document Evidence",
      fieldFinancialPosting: "Gate 1 Accounting Impact",
      noGlPostingBadge: "Zero GL / GRNI Postings",
      fieldReceivedBy: "Recorded By",
      fieldLineCustody: "Per-Line Custody & Base Quantity Conservation",
      colCustodyLine: "Item Line & UoM",
      colBaseSplit: "Delivered / Pending / Held / Released / Rejected",
      colValuedAccepted: "Valued Accepted (Gate 2)",
      colOd01Excess: "OD-01 Cap & Excess",
      dockCheckFormHeading: "Record Dock Inspection & Custody Disposition",
      dockCheckReadOnlyNote:
        "Read-only mode active; custody disposition controls are disabled for your role.",
      conservationFormulaLabel: "Conservation Law (Pending + Held + Released + Rejected)",
      conservationBalancedTag: "Matches Physical Delivery",
      conservationMismatchTag: "Must Equal Physically Delivered",
      od01CapLabel: "Approved PO Release Cap (OD-01)",
      od01CapExceededTag: "Exceeds approved PO cap — hold excess or amend PO first",
      presetReleaseWithinCap: "Release Within PO Cap",
      presetHoldAll: "Hold All",
      presetRejectAll: "Reject Unvalued",
      pendingQtyLabel: "Pending Check",
      heldQtyLabel: "Dock Held",
      releasedQtyLabel: "Released to Gate 2",
      rejectedQtyLabel: "Rejected Unvalued",
      inspectionNotesLabel: "Dock Inspection Notes",
      inspectionNotesPlaceholder: "e.g. 4 PCS crushed box rejected; 20 PCS released to Gate 2",
      evidenceRefLabel: "Inspection Evidence / Report Ref (Optional)",
      evidenceRefPlaceholder: "e.g. QC-MEMO-019",
      submitDockCheckButton: "Save Gate 1 Dock Inspection",
      submitDockCheckBusy: "Saving custody split...",
      dockCheckSuccessBanner: "Dock custody split and immutable audit trail updated with zero GL postings.",
      historyCardTitle: "Dock Inspection Audit Trail (Append-Only)",
      emptyHistoryBody: "No subsequent dock checks have been recorded after initial arrival.",
      colCheckedAt: "Checked At (WIB)",
      colTransition: "Custody Transition",
      colSplitSnapshot: "Split Snapshot (Base)",
      colReasonAndActor: "Reason, Notes & Inspector",
      receiptStatusLabels: {
        ARRIVED: "Arrived at Dock",
        IN_DOCK_INSPECTION: "In Dock Inspection",
        RELEASED_FOR_ACCEPTANCE: "Released to Gate 2",
        FULLY_REJECTED_UNVALUED: "Fully Rejected (Unvalued)",
      },
      custodyStatusLabels: {
        ARRIVED: "Arrived at Dock",
        DOCK_CHECK_PENDING: "Pending Dock Check",
        DOCK_HELD: "Held at Dock",
        DOCK_REJECTED: "Rejected Unvalued",
        DOCK_RELEASED: "Released to Gate 2",
      },
      discrepancyReasonLabels: {
        NONE: "No Discrepancy / Intact",
        DAMAGED_PACKAGING: "Damaged Packaging",
        WET_OR_DEFECTIVE_GOODS: "Wet or Defective Goods",
        OVERDELIVERY_OD01_HOLD: "Overdelivery Hold (OD-01)",
        WRONG_ITEM_OR_SPEC: "Wrong Item or Specification",
        MISSING_SUPPLIER_DOCS: "Missing Supplier Documents",
      },
      defaultReceivingStation: "Main Dock",
      hashPrefix: "Snapshot:",
      remainingCapPrefix: "Remaining cap",
      capPrefix: "Cap",
      discrepancyNotesLabel: "Discrepancy / Packaging Notes (Optional)",
      conditionEvidenceLabel: "Condition Evidence Ref (Optional)",
      splitCodes: {
        delivered: "Deliv",
        pending: "Pend",
        held: "Hold",
        released: "Rel",
        rejected: "Rej",
      },
      validation: {
        poRequired: "Select an approved purchase order before recording dock arrival.",
        deliveryNoteRequired: "Enter the supplier delivery note (Surat Jalan) number.",
        receivedDateRequired: "Select the physical arrival date.",
        stationRequired: "Enter the receiving dock or station identifier.",
        arrivalLinesRequired: "Enter a delivered quantity greater than zero on at least one PO line.",
        invalidQtyFormat: "Quantities must be non-negative decimals with up to 6 fractional digits.",
        conservationMismatch:
          "Pending + Held + Released + Rejected must equal the physically delivered base quantity.",
        discrepancyReasonRequired:
          "Select a discrepancy reason or provide notes whenever holding or rejecting goods.",
      },
      errors: {
        idempotency_key_required: "An idempotency key is required; reload the form and retry.",
        gr_lines_required: "Provide at least one PO line with delivered quantity above zero.",
        duplicate_po_line_in_arrival: "Each PO line can appear only once per dock arrival receipt.",
        invalid_discrepancy_reason: "Invalid discrepancy reason code supplied.",
        duplicate_receipt_idempotency_conflict:
          "This arrival key was already submitted with different delivery quantities.",
        duplicate_dock_check_idempotency_conflict:
          "This dock-check key was already used with a different split payload.",
        foreign_or_missing_purchase_order: "Purchase order was not found inside this legal entity.",
        po_not_eligible_for_receiving: "Purchase order is not open with an approved revision for receiving.",
        duplicate_supplier_delivery_note:
          "This supplier delivery note (Surat Jalan) is already recorded in this entity.",
        po_line_not_in_current_approved_revision:
          "A submitted line does not belong to the PO's current approved revision.",
        unsupported_uom_conversion: "No active UoM conversion exists between the received unit and base unit.",
        fractional_discrete_base_quantity:
          "Discrete base items cannot convert to a fractional base quantity.",
        od01_overdelivery_release_blocked:
          "OD-01 policy blocks releasing above approved PO quantity. Keep excess held at dock or approve a PO amendment first.",
        dock_quantity_conservation_mismatch:
          "Conservation mismatch: Pending + Held + Released + Rejected must equal Physically Delivered.",
        dock_release_below_valued_accepted:
          "Released quantity cannot drop below quantities already accepted at Gate 2.",
        insufficient_entity_permission:
          "Your active role grants do not permit recording arrivals or dock inspections.",
        concurrency_conflict: "Another update modified this receipt concurrently; reload and retry.",
      },
    },
  },
};

export function formatJakartaDateTime(isoUtc: string | null | undefined, locale: SupportedLocale): string {
  if (!isoUtc) return "—";
  const date = new Date(isoUtc);
  if (Number.isNaN(date.getTime())) return "—";
  return new Intl.DateTimeFormat(locale, {
    dateStyle: "medium",
    timeStyle: "medium",
    timeZone: "Asia/Jakarta",
  }).format(date);
}

export function formatIdr(amount: number): string {
  return new Intl.NumberFormat("id-ID", {
    style: "currency",
    currency: "IDR",
    maximumFractionDigits: 2,
  }).format(amount);
}
