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
      payables: string;
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
    lotPolicyLabel: string;
    lotPolicyLabels: Record<string, string>;
    updateLotPolicyButton: string;
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
    loadingDetail: string;
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
    stockStatusLabels: Record<string, string>;
    gate2: {
      sectionTitle: string;
      od04Notice: string;
      readOnlyPermissionNote: string;
      acceptanceDateLabel: string;
      stockStatusLabel: string;
      notesLabel: string;
      notesPlaceholder: string;
      colItem: string;
      colReleasedRemaining: string;
      colAcceptQty: string;
      previewButton: string;
      previewBusy: string;
      confirmButton: string;
      confirmBusy: string;
      previewStaleNotice: string;
      previewCardTitle: string;
      colPrevCost: string;
      colProjectedCost: string;
      colAcceptedAmt: string;
      journalPreviewTitle: string;
      sideLabels: Record<string, string>;
      blockedHeading: string;
      successPrefix: string;
      replaySuffix: string;
      openInventoryLink: string;
      linkedSectionTitle: string;
      emptyLinked: string;
      colAcceptanceRef: string;
      colLinkedRefs: string;
      colAcceptedTotal: string;
      gate1GlBadgePrefix: string;
      glBadgePrefix: string;
      locationLabel: string;
      locationAutoOption: string;
      lotNumberLabel: string;
      lotNumberPlaceholder: string;
      lotRequiredBadge: string;
      lotDisabledNote: string;
      expiryDateLabel: string;
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
      acceptanceLinesRequired: string;
    };
    errors: Record<string, string>;
  };
  inventory: {
    pageTitle: string;
    backToOverview: string;
    loadErrorTitle: string;
    retryButton: string;
    od04PolicyBanner: string;
    od10PolicyBanner: string;
    tabListLabel: string;
    tabs: {
      positions: string;
      inspections: string;
      accruals: string;
      movements: string;
    };
    stats: {
      stockedItemsWithBalance: string;
      carryingValueTotal: string;
      openGrniTotal: string;
      restrictedItemCount: string;
    };
    filterSearchLabel: string;
    filterSearchPlaceholder: string;
    filterStatusLabel: string;
    allStockStatuses: string;
    refreshButton: string;
    positionsCardTitle: string;
    emptyPositionsTitle: string;
    emptyPositionsBody: string;
    colItem: string;
    colBuckets: string;
    colValuedQty: string;
    colMovingAverageCost: string;
    colPoolStatus: string;
    colAction: string;
    inspectLedgerButton: string;
    activeLedgerButton: string;
    bucketCodes: {
      unrestricted: string;
      qi: string;
      blocked: string;
      available: string;
    };
    poolStatusLabels: Record<string, string>;
    stockStatusLabels: Record<string, string>;
    accrualStatusLabels: Record<string, string>;
    movementTypeLabels: Record<string, string>;
    entryTypeLabels: Record<string, string>;
    accrualsCardTitle: string;
    emptyAccrualsTitle: string;
    emptyAccrualsBody: string;
    colAccrualRef: string;
    colSourceChain: string;
    colSupplier: string;
    colOpenVsAccrued: string;
    colAccrualStatus: string;
    movementsCardTitle: string;
    emptyMovementsTitle: string;
    emptyMovementsBody: string;
    colMovementRef: string;
    colMovementType: string;
    colMovementQtyVal: string;
    colMovementActor: string;
    ledgerCardTitle: string;
    loadingLedger: string;
    selectItemPrompt: string;
    emptyLedgerEntries: string;
    colLedgerSeq: string;
    colLedgerSource: string;
    colLedgerDelta: string;
    colLedgerBeforeAfterCost: string;
    colLedgerCarrying: string;
    openReceivingLink: string;
    zoneTypeLabels: Record<string, string>;
    lotPolicyLabels: Record<string, string>;
    inspectionStatusLabels: Record<string, string>;
    reservationStatusLabels: Record<string, string>;
    reservationSourceLabels: Record<string, string>;
    qualityReasonLabels: Record<string, string>;
    inspectionsTab: {
      cardTitle: string;
      emptyTitle: string;
      emptyBody: string;
      colInspectionRef: string;
      colSourceLot: string;
      colQuantities: string;
      colStatusAction: string;
      openItemButton: string;
      releasedPrefix: string;
      blockedPrefix: string;
      pendingPrefix: string;
    };
    detailSheet: {
      sectionTitle: string;
      loadingDetail: string;
      selectPrompt: string;
      reconciledBadge: string;
      unreconciledBadge: string;
      lotPolicyPrefix: string;
      metricTotalValued: string;
      metricUnrestricted: string;
      metricQualityInspection: string;
      metricBlocked: string;
      metricReserved: string;
      metricAvailable: string;
      subTabOperations: string;
      subTabTimeline: string;
      balancesTitle: string;
      emptyBalances: string;
      colLocationZone: string;
      colLotExpiry: string;
      colStockStatus: string;
      colOnHandReservedAvailable: string;
      operationsTitle: string;
      qualityReadOnlyNotice: string;
      warehouseManageReadOnlyNotice: string;
      dispositionCardTitle: string;
      inspectionSelectLabel: string;
      releaseQtyLabel: string;
      releaseLocationLabel: string;
      blockQtyLabel: string;
      blockLocationLabel: string;
      reasonCodeLabel: string;
      evidenceRefLabel: string;
      notesLabel: string;
      submitDispositionBusy: string;
      submitDispositionButton: string;
      dispositionSuccessPrefix: string;
      invalidDispositionQtyError: string;
      invalidPositiveQtyError: string;
      noPendingInspections: string;
      transferCardTitle: string;
      sourceBalanceLabel: string;
      emptyMovableBalancesOption: string;
      targetLocationLabel: string;
      emptyTransferTargetOption: string;
      transferQtyLabel: string;
      postingDateLabel: string;
      submitTransferBusy: string;
      submitTransferButton: string;
      transferSuccessPrefix: string;
      statusTransitionCardTitle: string;
      fromStatusLabel: string;
      toStatusLabel: string;
      fromLocationLabel: string;
      toLocationLabel: string;
      statusQtyLabel: string;
      submitStatusBusy: string;
      submitStatusButton: string;
      statusSuccessPrefix: string;
      reservationCardTitle: string;
      resLocationLabel: string;
      emptyReserveBalanceOption: string;
      resLotLabel: string;
      resSourceTypeLabel: string;
      resSourceDocLabel: string;
      resQtyLabel: string;
      resNotesLabel: string;
      submitReserveBusy: string;
      submitReserveButton: string;
      reserveSuccessPrefix: string;
      reservationsTableTitle: string;
      emptyReservations: string;
      releaseQtyPlaceholder: string;
      releaseButton: string;
      releaseAllButton: string;
      releaseBusy: string;
      colResRef: string;
      colResSource: string;
      colResQty: string;
      colResStatus: string;
      warehouseToggleLabel: string;
      warehouseCodeLabel: string;
      warehouseNameLabel: string;
      createWarehouseButton: string;
      warehouseSelectLabel: string;
      locationCodeLabel: string;
      locationNameLabel: string;
      zoneTypeLabel: string;
      defaultReceivingLabel: string;
      createLocationButton: string;
      createBusy: string;
      warehouseCreatedMsg: string;
      locationCreatedMsg: string;
      timelineTitle: string;
      emptyTimeline: string;
      colTimelineRef: string;
      colTimelineLocationLot: string;
      colTimelineDelta: string;
      colTimelineCost: string;
      issueCardTitle: string;
      issueHint: string;
      issueSourceLabel: string;
      issueLocationLabel: string;
      issueQtyLabel: string;
      issueNotesLabel: string;
      issueSubmitButton: string;
      issueSubmitBusy: string;
      issueSuccessPrefix: string;
      issueLotSelectLabel: string;
      noLotOption: string;
      emptyIssueBalanceOption: string;
    };
    errors: Record<string, string>;
  };
  payables: {
    pageTitle: string;
    backToOverview: string;
    loadErrorTitle: string;
    retryButton: string;
    accessDeniedTitle: string;
    accessDeniedBody: string;
    readOnlyBanner: string;
    od03GovernanceTitle: string;
    od03GovernanceBody: string;
    od08GovernanceBody: string;
    od07TaxDisclaimer: string;
    tabListLabel: string;
    tabs: {
      bills: string;
      grniQueue: string;
      subledger: string;
      policyTax: string;
    };
    stats: {
      openPayableLiability: string;
      openGrniPool: string;
      pendingApprovalBills: string;
      blockedPayables: string;
    };
    refreshButton: string;
    newBillButton: string;
    filterSearchLabel: string;
    filterSearchPlaceholder: string;
    billsCardTitle: string;
    emptyBillsTitle: string;
    emptyBillsBody: string;
    grniQueueCardTitle: string;
    emptyGrniTitle: string;
    emptyGrniBody: string;
    subledgerCardTitle: string;
    emptyPayablesTitle: string;
    emptyPayablesBody: string;
    priceAdjustmentsTitle: string;
    emptyPriceAdjustments: string;
    policySectionTitle: string;
    taxTableTitle: string;
    createBillAction: string;
    composer: {
      titleNew: string;
      titleEditPrefix: string;
      cancelButton: string;
      poSelectLabel: string;
      emptyPoOption: string;
      accrualLineSelectLabel: string;
      emptyAccrualOption: string;
      billTypeLabel: string;
      vendorInvoiceLabel: string;
      taxInvoiceLabel: string;
      withholdingSlipLabel: string;
      invoiceDateLabel: string;
      dueDateLabel: string;
      postingDateLabel: string;
      declaredGrossLabel: string;
      syncComputedGrossButton: string;
      billedQtyLabel: string;
      invoiceUnitPriceLabel: string;
      ppnCodeLabel: string;
      pphCodeLabel: string;
      noneOption: string;
      selectOptionPlaceholder: string;
      notesLabel: string;
      previewButton: string;
      previewBusy: string;
      saveDraftButton: string;
      updateDraftButton: string;
      saveBusy: string;
      previewTitle: string;
      comparisonTitle: string;
      colOrderedAcceptedOpen: string;
      colBilledPrice: string;
      colVarianceSplit: string;
      colTaxes: string;
      journalPreviewTitle: string;
      colJournalSide: string;
      colJournalAccount: string;
      colJournalAmount: string;
      sideDebit: string;
      sideCredit: string;
      coveredStockLabel: string;
      uncoveredPpvLabel: string;
      projectedCostLabel: string;
      bdDiffLabel: string;
      netApLabel: string;
      stalePreviewNotice: string;
    };
    detail: {
      sectionTitle: string;
      closeButton: string;
      headerRef: string;
      headerVendor: string;
      headerDates: string;
      headerMatchStatus: string;
      headerPaymentBlock: string;
      headerTotals: string;
      colAction: string;
      openDetailButton: string;
      activeDetailButton: string;
      colLineVarianceOutcome: string;
      editDraftButton: string;
      blockingReasonsTitle: string;
      linesTitle: string;
      adjustmentsTitle: string;
      taxesTitle: string;
      evaluationsTitle: string;
      submitButton: string;
      submitBusy: string;
      approveButton: string;
      rejectButton: string;
      decideBusy: string;
      approveVarianceCheckbox: string;
      applyBlockCheckbox: string;
      decisionReasonLabel: string;
      decisionReasonPlaceholder: string;
      blockReasonLabel: string;
      sodSelfApprovalNotice: string;
      postSectionTitle: string;
      postingDateLabel: string;
      postButton: string;
      postBusy: string;
      blockControlTitle: string;
      holdPaymentButton: string;
      releasePaymentButton: string;
      blockBusy: string;
      blockActionReasonLabel: string;
      noPermSubmitHint: string;
      noPermApproveHint: string;
      noPermPostHint: string;
      defaultApproveReason: string;
      defaultRejectReason: string;
      defaultManualHoldReason: string;
      defaultBlockHoldReason: string;
      defaultReleaseReason: string;
    };
    micro: {
      unbilledGrniShort: string;
      poolOnHandShort: string;
      onHandBeforeBillShort: string;
      coveredShort: string;
      uncoveredShort: string;
      inv1300Short: string;
      ppv5200Short: string;
      openShort: string;
      dueShort: string;
      invShort: string;
      whtShort: string;
      varShort: string;
      netShort: string;
      macShort: string;
      ppToleranceShort: string;
      dqToleranceShort: string;
      bdLimitShort: string;
      effectiveShort: string;
    };
    evaluationStageLabels: Record<string, string>;
    billStatusLabels: Record<string, string>;
    matchStatusLabels: Record<string, string>;
    dimensionOutcomeLabels: Record<string, string>;
    paymentBlockLabels: Record<string, string>;
    payableStatusLabels: Record<string, string>;
    taxRoleLabels: Record<string, string>;
    taxFamilyLabels: Record<string, string>;
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
        payables: "Hutang Usaha (AP)",
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
      availableState: "Tersedia (S01–S07)",
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
        payables: "Hutang Usaha (AP), Match 3-Arah & Selisih Harga",
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
      lotPolicyLabel: "Kebijakan Pelacakan Lot / Batch",
      lotPolicyLabels: {
        NONE: "Tanpa Pelacakan Lot",
        OPTIONAL: "Lot Opsional",
        REQUIRED: "Lot Wajib (S08)",
      },
      updateLotPolicyButton: "Terapkan Kebijakan Lot",
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
        unlotted_stock_prevents_required_lot_policy:
          "Saldo stok tanpa lot masih ada pada entitas aktif; kebijakan wajib lot ditolak.",
        non_stock_item_cannot_enable_lot_tracking: "Barang jasa non-stok tidak dapat mengaktifkan pelacakan lot.",
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
      pageTitle: "Penerimaan Dermaga (Gate 1) & Valuasi (Gate 2)",
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
      loadingDetail: "Memuat rincian penerimaan...",
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
        PARTIALLY_ACCEPTED: "Sebagian Diterima (Bernilai)",
        VALUED_ACCEPTED: "Diterima & Divaluasi",
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
      stockStatusLabels: {
        UNRESTRICTED: "Stok Bebas Pakai (Unrestricted)",
        QUALITY_INSPECTION: "Karantina Inspeksi Mutu (QI)",
        BLOCKED: "Stok Diblokir (Blocked)",
      },
      gate2: {
        sectionTitle: "Penerimaan Bernilai Gate 2 — Valuasi Rata-Rata Bergerak & GRNI",
        od04Notice:
          "Aturan OD-04 aktif (ATH-CR-004): penerimaan bernilai memposting Dr Persediaan / Cr GRNI dalam satu transaksi atomik dan menghitung ulang biaya rata-rata bergerak secara presisi.",
        readOnlyPermissionNote:
          "Akun Anda belum memiliki izin receiving.accept pada entitas ini; pratinjau valuasi tersedia dalam mode baca.",
        acceptanceDateLabel: "Tanggal Posting Penerimaan Bernilai",
        stockStatusLabel: "Status Ketersediaan Stok Tujuan",
        notesLabel: "Catatan Penerimaan Bernilai (Opsional)",
        notesPlaceholder: "Contoh: Batch 2 diterima di gudang utama",
        colItem: "Baris & Barang (Satuan Dasar)",
        colReleasedRemaining: "Sisa Rilis Gate 1",
        colAcceptQty: "Kuantitas Diterima (Dasar)",
        previewButton: "Hitung Pratinjau Valuasi & GL",
        previewBusy: "Menghitung pratinjau...",
        confirmButton: "Konfirmasi Penerimaan Bernilai",
        confirmBusy: "Memposting Persediaan & GRNI...",
        previewStaleNotice:
          "Angka atau tanggal berubah. Hitung ulang pratinjau sebelum melakukan posting permanen.",
        previewCardTitle: "Pratinjau Dampak Rata-Rata Bergerak & Jurnal GL",
        colPrevCost: "Saldo / HPP Sebelum",
        colProjectedCost: "Saldo / HPP Proyeksi",
        colAcceptedAmt: "Nilai Diterima (IDR)",
        journalPreviewTitle: "Pratinjau Entri Jurnal Seimbang (Dr / Cr)",
        sideLabels: {
          DEBIT: "Debit",
          CREDIT: "Kredit",
        },
        blockedHeading: "Posting Diblokir Kebijakan (OD-04 / Periode / Kustodi)",
        successPrefix: "Penerimaan bernilai diposting permanen:",
        replaySuffix: "(Putar ulang idempoten tanpa duplikasi stok atau jurnal GL.)",
        openInventoryLink: "Lihat di Persediaan & Valuasi",
        linkedSectionTitle: "Riwayat Penerimaan Bernilai Gate 2 pada GRN Ini",
        emptyLinked: "Belum ada penerimaan bernilai Gate 2 pada dokumen kedatangan ini.",
        colAcceptanceRef: "Nomor Penerimaan & Tanggal",
        colLinkedRefs: "Referensi Mutasi, GRNI & Jurnal GL",
        colAcceptedTotal: "Kuantitas & Nilai (IDR)",
        gate1GlBadgePrefix: "GL Gate 1:",
        glBadgePrefix: "GL Gate 2:",
        locationLabel: "Lokasi Penyimpanan Tujuan (Opsional)",
        locationAutoOption: "Lokasi Bawaan Sesuai Status Stok",
        lotNumberLabel: "Nomor Lot / Batch",
        lotNumberPlaceholder: "Contoh: LOT-JKT-2611-01",
        lotRequiredBadge: "Lot Wajib",
        lotDisabledNote: "Tanpa pelacakan lot",
        expiryDateLabel: "Tanggal Kedaluwarsa (Opsional)",
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
        acceptanceLinesRequired:
          "Masukkan kuantitas terima lebih besar dari nol (maksimal sesuai sisa rilis Gate 1).",
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
        acceptance_lines_required:
          "Masukkan kuantitas penerimaan Gate 2 minimal pada satu baris rilis.",
        invalid_stock_status: "Status stok tujuan tidak valid.",
        receipt_fully_rejected_unvalued:
          "Penerimaan yang seluruhnya ditolak tanpa nilai tidak memiliki sisa rilis untuk Gate 2.",
        non_stocked_item_not_inventory_eligible:
          "Barang non-stok atau jasa tidak masuk buku besar persediaan.",
        acceptance_qty_must_be_positive: "Kuantitas penerimaan bernilai harus lebih besar dari nol.",
        acceptance_exceeds_released_quantity:
          "Kuantitas melebihi saldo yang sudah dirilis Gate 1; barang ditahan atau ditolak tidak boleh masuk persediaan bernilai.",
        od04_zero_or_negative_acceptance_value_blocked:
          "Kebijakan OD-04 memblokir penerimaan bernilai nol atau negatif.",
        od04_anomalous_valuation_pool_blocked:
          "Pool valuasi barang sedang diblokir oleh aturan keamanan OD-04 dan memerlukan keputusan Pemilik Produk (ATH-CR-004).",
        duplicate_acceptance_idempotency_conflict:
          "Kunci idempotensi Gate 2 sudah dipakai untuk muatan penerimaan yang berbeda.",
        missing_inventory_asset_mapping:
          "Pemetaan akun GL Persediaan (1300) belum dikonfigurasi pada modul Keuangan.",
        missing_grni_clearing_mapping:
          "Pemetaan akun GL Kliring GRNI (2110) belum dikonfigurasi pada modul Keuangan.",
        period_closed:
          "Periode fiskal pada tanggal posting sudah ditutup (CLOSED) dan menolak posting akrual.",
        period_not_found_for_date:
          "Periode fiskal belum dibuat untuk tanggal posting yang dipilih.",
        insufficient_entity_permission:
          "Akun Anda tidak memiliki izin melakukan aksi ini pada entitas hukum ini.",
        concurrency_conflict: "Rekaman baru saja diubah transaksi lain secara bersamaan; muat ulang sebelum mencoba lagi.",
        lot_required_by_item_policy:
          "Barang ini mewajibkan nomor lot pada penerimaan bernilai Gate 2.",
        lot_not_enabled_for_item: "Barang ini dikonfigurasi tanpa pelacakan lot.",
        location_zone_mismatch:
          "Zona lokasi tujuan tidak sesuai dengan status stok yang dipilih.",
        location_not_found: "Lokasi gudang tidak ditemukan pada entitas ini.",
        location_inactive: "Lokasi gudang yang dipilih sedang nonaktif.",
      },
    },
    inventory: {
      pageTitle: "Persediaan, Valuasi Rata-Rata Bergerak & Akrual GRNI",
      backToOverview: "Ringkasan Entitas",
      loadErrorTitle: "Gagal memuat buku besar persediaan entitas",
      retryButton: "Muat Ulang Data Persediaan",
      od04PolicyBanner:
        "Kebijakan OD-04 Aktif (ATH-CR-004): setiap barang dihitung dengan rata-rata bergerak per entitas (IDR); stok inspeksi mutu & blokir tetap dinilai di buku besar namun bersaldo tersedia nol untuk pemenuhan.",
      od10PolicyBanner:
        "Kebijakan OD-10 Aktif (ATH-CR-005): stok negatif dan reservasi melebihi saldo bebas pakai ditolak tegas; pemindahan lokasi dan disposisi inspeksi mutu menjaga nilai buku tetap utuh tanpa jurnal GL baru.",
      tabListLabel: "Tampilan modul persediaan",
      tabs: {
        positions: "Saldo & Valuasi Rata-Rata Bergerak",
        inspections: "Antrean Inspeksi Mutu",
        accruals: "Akrual GRNI Terbuka",
        movements: "Pergerakan Stok Terbaru",
      },
      stats: {
        stockedItemsWithBalance: "Barang Stok Ber-Saldo",
        carryingValueTotal: "Total Nilai Buku Persediaan",
        openGrniTotal: "Total Akrual GRNI Terbuka",
        restrictedItemCount: "Item Berstatus Dibatasi (QI / Blokir)",
      },
      filterSearchLabel: "Cari SKU, referensi GRNI, atau PO",
      filterSearchPlaceholder: "Cari kode SKU, GRNI, atau PO...",
      filterStatusLabel: "Saring bucket status stok",
      allStockStatuses: "Semua Status Stok",
      refreshButton: "Segarkan",
      positionsCardTitle: "Posisi Stok & Biaya Rata-Rata Bergerak (Perpetual)",
      emptyPositionsTitle: "Belum Ada Saldo Persediaan Bernilai",
      emptyPositionsBody:
        "Rekam dan konfirmasi penerimaan bernilai Gate 2 pada modul Penerimaan Gudang untuk mengisi posisi stok dan pool valuasi entitas ini.",
      colItem: "Kode & Nama Barang",
      colBuckets: "Bucket Status Stok & Tersedia",
      colValuedQty: "Total Dinilai",
      colMovingAverageCost: "HPP Rata-Rata & Nilai Buku",
      colPoolStatus: "Status Pool",
      colAction: "Buku Besar",
      inspectLedgerButton: "Buka Buku Besar",
      activeLedgerButton: "Buku Besar Dibuka",
      bucketCodes: {
        unrestricted: "Bebas",
        qi: "Inspeksi Mutu",
        blocked: "Diblokir",
        available: "Siap Pakai",
      },
      poolStatusLabels: {
        ACTIVE: "Pool Aktif",
        BLOCKED: "Diblokir OD-04",
      },
      stockStatusLabels: {
        UNRESTRICTED: "Bebas Pakai",
        QUALITY_INSPECTION: "Inspeksi Mutu",
        BLOCKED: "Diblokir",
      },
      accrualStatusLabels: {
        OPEN: "Terbuka (Belum Ditagih)",
        PARTIALLY_CLEARED: "Sebagian Dikliring",
        CLEARED: "Lunas Dikliring",
      },
      movementTypeLabels: {
        PURCHASE_RECEIPT_ACCEPTANCE: "Penerimaan Pembelian (Gate 2)",
        LOCATION_TRANSFER: "Pemindahan Lokasi Gudang",
        QUALITY_STATUS_CHANGE: "Perubahan Status Mutu / Pembatasan",
        OPERATIONAL_ISSUE: "Pemakaian / Pengeluaran Stok Operasional",
      },
      entryTypeLabels: {
        RECEIPT_ACCEPTANCE: "Penerimaan Bernilai",
        OPERATIONAL_ISSUE: "Pengeluaran Stok Operasional",
        INVOICE_PRICE_ADJUSTMENT: "Penyesuaian Harga Faktur (S09)",
      },
      accrualsCardTitle: "Sub-Buku Besar Akrual GRNI (Menunggu Pencocokan 3-Arah S09)",
      emptyAccrualsTitle: "Belum Ada Akrual GRNI Terbuka",
      emptyAccrualsBody:
        "Akrual GRNI terbentuk otomatis ketika penerimaan barang dirilis ke Gate 2 dan diterima bernilai.",
      colAccrualRef: "Nomor GRNI & Tanggal",
      colSourceChain: "Rantai Sumber (Gate 2 / GRN / PO / GL)",
      colSupplier: "Pemasok",
      colOpenVsAccrued: "Sisa Terbuka & Nilai Akrual",
      colAccrualStatus: "Status GRNI",
      movementsCardTitle: "Riwayat Pergerakan Stok Append-Only",
      emptyMovementsTitle: "Belum Ada Mutasi Stok",
      emptyMovementsBody: "Belum terdapat transaksi mutasi persediaan pada entitas ini.",
      colMovementRef: "Nomor Mutasi & Tanggal",
      colMovementType: "Jenis & Sumber Dokumen",
      colMovementQtyVal: "Kuantitas & Valuasi (IDR)",
      colMovementActor: "Operator Pelaksana",
      ledgerCardTitle: "Buku Besar Valuasi Rata-Rata Bergerak Per Item",
      loadingLedger: "Memuat buku besar valuasi...",
      selectItemPrompt: "Pilih baris barang pada tabel posisi stok di atas untuk menelusuri riwayat HPP dan tautan jurnal GL.",
      emptyLedgerEntries: "Belum terdapat entri buku besar valuasi untuk barang ini.",
      colLedgerSeq: "Urutan",
      colLedgerSource: "Tanggal, Sumber & Jurnal GL",
      colLedgerDelta: "Delta Qty · Biaya Satuan · Nilai",
      colLedgerBeforeAfterCost: "HPP Sebelum → Sesudah",
      colLedgerCarrying: "Qty Akhir & Nilai Buku",
      openReceivingLink: "Buka Penerimaan Gudang",
      zoneTypeLabels: {
        RECEIVING_STAGING: "Area Penerimaan",
        STORAGE: "Rak Penyimpanan",
        QUARANTINE: "Zona Karantina Mutu",
        BLOCKED_HOLD: "Area Barang Diblokir",
        DISPATCH: "Area Pengiriman",
      },
      lotPolicyLabels: {
        NONE: "Tanpa Lot",
        OPTIONAL: "Lot Opsional",
        REQUIRED: "Lot Wajib",
      },
      inspectionStatusLabels: {
        PENDING: "Menunggu Inspeksi",
        PARTIALLY_DECIDED: "Sebagian Diputuskan",
        COMPLETED: "Selesai",
      },
      reservationStatusLabels: {
        ACTIVE: "Aktif",
        PARTIALLY_CONSUMED: "Sebagian Terpakai",
        FULFILLED: "Terpenuhi",
        RELEASED: "Dilepaskan",
      },
      reservationSourceLabels: {
        SALES_ORDER: "Pesanan Penjualan",
        MANUAL_HOLD: "Penahanan Manual",
        FULFILLMENT: "Pemenuhan Gudang",
      },
      qualityReasonLabels: {
        PASSED_SPECIFICATION: "Lulus Uji Spesifikasi",
        DAMAGED_OR_CONTAMINATED: "Kemasan Rusak / Terkontaminasi",
        SPEC_DEVIATION_HOLD: "Tahan Deviasi Spesifikasi",
        MISSING_CERTIFICATE: "Dokumen Sertifikat / COA Kurang",
        SUPERVISOR_REEVALUATION: "Evaluasi Ulang Pengawas",
      },
      inspectionsTab: {
        cardTitle: "Antrean Inspeksi Mutu Penerimaan (Gate 2 Karantina)",
        emptyTitle: "Tidak Ada Antrean Inspeksi Mutu",
        emptyBody: "Penerimaan Gate 2 dengan status Inspeksi Mutu akan otomatis muncul di sini untuk disposisi rilis atau blokir.",
        colInspectionRef: "Nomor Inspeksi & Sumber",
        colSourceLot: "Barang, Lokasi Karantina & Lot",
        colQuantities: "Ringkasan Kuantitas (Pending / Rilis / Blokir)",
        colStatusAction: "Status & Tindak Lanjut",
        openItemButton: "Buka Rincian Stok & Disposisi",
        releasedPrefix: "Dirilis",
        blockedPrefix: "Diblokir",
        pendingPrefix: "Menunggu",
      },
      detailSheet: {
        sectionTitle: "Rincian Ketersediaan Stok, Lokasi, Lot & Operasi",
        loadingDetail: "Memuat rincian lokasi, lot, inspeksi, dan reservasi...",
        selectPrompt: "Pilih baris barang pada tabel posisi stok di atas untuk mengelola lokasi, lot, disposisi mutu, dan reservasi.",
        reconciledBadge: "Terekonsiliasi",
        unreconciledBadge: "Perlu Pemeriksaan Rekonsiliasi",
        lotPolicyPrefix: "Kebijakan Lot",
        metricTotalValued: "Total Dinilai",
        metricUnrestricted: "Bebas Pakai",
        metricQualityInspection: "Karantina Mutu",
        metricBlocked: "Diblokir",
        metricReserved: "Direservasi",
        metricAvailable: "Tersedia Pemenuhan",
        subTabOperations: "Lokasi, Lot & Operasi",
        subTabTimeline: "Status & Linimasa Mutasi",
        balancesTitle: "Saldo per Lokasi Gudang, Lot & Status",
        emptyBalances: "Belum ada rincian saldo lokasi pada barang ini.",
        colLocationZone: "Gudang & Lokasi (Zona)",
        colLotExpiry: "Nomor Lot & Kedaluwarsa",
        colStockStatus: "Status Stok",
        colOnHandReservedAvailable: "Di Tangan · Direservasi · Tersedia",
        operationsTitle: "Panel Operasi Persediaan & Mutu",
        qualityReadOnlyNotice:
          "Mode baca mutu: Anda memiliki izin operasional gudang (inventory.manage) tanpa izin inspeksi mutu (quality.inspect), sehingga keputusan rilis/blokir dikunci.",
        warehouseManageReadOnlyNotice:
          "Mode baca saja: Anda tidak memiliki izin inventory.manage pada entitas ini untuk memindahkan stok, membuat reservasi, atau menambah lokasi.",
        dispositionCardTitle: "Keputusan Disposisi Inspeksi Mutu",
        inspectionSelectLabel: "Dokumen Inspeksi Antrean",
        releaseQtyLabel: "Qty Rilis ke Bebas Pakai",
        releaseLocationLabel: "Lokasi Rak Rilis",
        blockQtyLabel: "Qty Diblokir",
        blockLocationLabel: "Lokasi Blokir",
        reasonCodeLabel: "Kode Alasan Mutu",
        evidenceRefLabel: "Ref Sertifikat / COA (Opsional)",
        notesLabel: "Catatan Keputusan / Mutasi (Opsional)",
        submitDispositionBusy: "Menyimpan Disposisi...",
        submitDispositionButton: "Catat Disposisi Mutu",
        dispositionSuccessPrefix: "Disposisi mutu tercatat permanen melalui mutasi",
        invalidDispositionQtyError: "Masukkan kuantitas rilis dan/atau blokir bernilai positif (total > 0).",
        invalidPositiveQtyError: "Masukkan kuantitas bernilai positif (> 0).",
        noPendingInspections: "Tidak ada inspeksi karantina yang menunggu keputusan pada item ini.",
        transferCardTitle: "Pemindahan Lokasi Dalam Gudang (Tanpa Mengubah Nilai Buku)",
        sourceBalanceLabel: "Saldo & Lokasi Asal",
        emptyMovableBalancesOption: "Tidak ada saldo lokasi ber-stok",
        targetLocationLabel: "Lokasi Tujuan (Gudang Sama)",
        emptyTransferTargetOption: "Tidak ada lokasi tujuan berzona kompatibel",
        transferQtyLabel: "Kuantitas Pindah",
        postingDateLabel: "Tanggal Mutasi",
        submitTransferBusy: "Memindahkan Stok...",
        submitTransferButton: "Pindahkan Lokasi",
        transferSuccessPrefix: "Stok dipindahkan melalui mutasi",
        statusTransitionCardTitle: "Penahanan / Pembukaan Blokir Status Manual (Mutu)",
        fromStatusLabel: "Dari Status",
        toStatusLabel: "Ke Status",
        fromLocationLabel: "Lokasi Asal",
        toLocationLabel: "Lokasi Tujuan",
        statusQtyLabel: "Kuantitas Ubah Status",
        submitStatusBusy: "Memproses Perubahan Status...",
        submitStatusButton: "Terapkan Perubahan Status",
        statusSuccessPrefix: "Status stok diperbarui melalui mutasi",
        reservationCardTitle: "Buat Reservasi Stok Eksplisit (OD-10)",
        resLocationLabel: "Lokasi Bebas Pakai",
        emptyReserveBalanceOption: "Tidak ada saldo Bebas Pakai tersedia untuk direservasi",
        resLotLabel: "Lot (Bila Ada)",
        resSourceTypeLabel: "Jenis Sumber",
        resSourceDocLabel: "Nomor Dokumen Sumber",
        resQtyLabel: "Kuantitas Direservasi",
        resNotesLabel: "Catatan Reservasi (Opsional)",
        submitReserveBusy: "Membuat Reservasi...",
        submitReserveButton: "Kunci Reservasi Stok",
        reserveSuccessPrefix: "Reservasi dibuat:",
        reservationsTableTitle: "Daftar Reservasi Aktif & Riwayat Pelepasan",
        emptyReservations: "Belum ada reservasi tercatat untuk item ini.",
        releaseQtyPlaceholder: "Kosongkan = semua",
        releaseButton: "Lepas Sebagian / Nilai",
        releaseAllButton: "Lepas Semua",
        releaseBusy: "Melepas...",
        colResRef: "Nomor Reservasi & Tanggal",
        colResSource: "Sumber & Lokasi / Lot",
        colResQty: "Reservasi · Lepas · Sisa",
        colResStatus: "Status & Aksi",
        warehouseToggleLabel: "Kelola Gudang & Lokasi Tambahan",
        warehouseCodeLabel: "Kode Gudang",
        warehouseNameLabel: "Nama Gudang",
        createWarehouseButton: "Tambah Gudang",
        warehouseSelectLabel: "Gudang Induk",
        locationCodeLabel: "Kode Lokasi",
        locationNameLabel: "Nama Lokasi",
        zoneTypeLabel: "Zona Lokasi",
        defaultReceivingLabel: "Lokasi default penerimaan",
        createLocationButton: "Simpan Lokasi Baru",
        createBusy: "Menyimpan...",
        warehouseCreatedMsg: "Gudang baru berhasil dibuat:",
        locationCreatedMsg: "Lokasi gudang baru berhasil dibuat:",
        timelineTitle: "Linimasa Mutasi Status, Lokasi & Valuasi Append-Only",
        emptyTimeline: "Belum ada riwayat mutasi stok untuk item ini.",
        colTimelineRef: "Nomor Mutasi & Tanggal",
        colTimelineLocationLot: "Lokasi, Zona & Lot",
        colTimelineDelta: "Status & Delta Kuantitas",
        colTimelineCost: "HPP Satuan · Nilai Buku Delta",
        issueCardTitle: "Pengeluaran Stok Operasional (Dr 5100 HPP / Cr 1300 Persediaan)",
        issueHint:
          "Keluarkan stok berstatus Bebas Pakai dan tidak tereservasi pada HPP rata-rata bergerak saat ini.",
        issueSourceLabel: "Referensi Pemakaian",
        issueLocationLabel: "Saldo Lokasi & Lot",
        issueQtyLabel: "Kuantitas Keluar",
        issueNotesLabel: "Catatan Pemakaian",
        issueSubmitButton: "Posting Pengeluaran Stok",
        issueSubmitBusy: "Memposting pengeluaran...",
        issueSuccessPrefix: "Pengeluaran stok tercatat:",
        issueLotSelectLabel: "Lot",
        noLotOption: "Tanpa Lot",
        emptyIssueBalanceOption: "Tidak ada saldo Bebas Pakai tersedia untuk dikeluarkan.",
      },
      errors: {
        od10_insufficient_available_stock:
          "Kebijakan OD-10 menolak transaksi: saldo bebas pakai tidak mencukupi (terpotong reservasi aktif atau stok dibatasi).",
        od10_negative_stock_forbidden:
          "Kebijakan OD-10 melarang keras saldo stok negatif pada lokasi maupun posisi entitas.",
        od10_restricted_stock_not_reservable:
          "Hanya stok berstatus Bebas Pakai yang dapat direservasi untuk pemenuhan.",
        pending_receipt_inspection_location_locked:
          "Stok karantina masih terkunci pada antrean inspeksi mutu; gunakan panel Disposisi Mutu untuk merilis atau memblokir.",
        location_zone_mismatch:
          "Zona lokasi tujuan tidak kompatibel dengan status stok yang dipilih.",
        disposition_exceeds_pending_quantity:
          "Jumlah rilis ditambah blokir melebihi sisa kuantitas karantina yang menunggu keputusan.",
        duplicate_warehouse_code: "Kode gudang sudah terdaftar pada entitas hukum ini.",
        duplicate_location_code: "Kode lokasi sudah terdaftar pada entitas hukum ini.",
        duplicate_disposition_idempotency_conflict:
          "Kunci idempotensi disposisi mutu sudah digunakan dengan muatan berbeda.",
        duplicate_reservation_idempotency_conflict:
          "Kunci idempotensi reservasi sudah digunakan dengan muatan berbeda.",
        duplicate_movement_idempotency_conflict:
          "Kunci idempotensi mutasi stok sudah digunakan dengan muatan berbeda.",
        insufficient_entity_permission:
          "Peran aktif Anda tidak memiliki izin untuk menjalankan tindakan tersebut pada entitas ini.",
        period_closed:
          "Periode fiskal pada tanggal posting berstatus ditutup.",
      },
    },
    payables: {
      pageTitle: "Hutang Usaha (AP), Pencocokan 3-Arah & Selisih Harga",
      backToOverview: "Ringkasan Entitas",
      loadErrorTitle: "Gagal memuat modul Hutang Usaha entitas",
      retryButton: "Muat Ulang Data AP",
      accessDeniedTitle: "Akses Modul Hutang Usaha Ditolak",
      accessDeniedBody:
        "Izin entitas aktif Anda tidak mencakup pembacaan atau pengelolaan faktur pemasok (ap.read).",
      readOnlyBanner:
        "Mode baca saja: Anda memiliki izin baca AP tanpa izin ubah, persetujuan selisih harga, atau posting jurnal.",
      od03GovernanceTitle: "Tata Kelola OD-03 & OD-08 Aktif (ATH-CR-006)",
      od03GovernanceBody:
        "Toleransi otomatis PP/DQ/BD = 0 (Match Ketat). Kelebihan tagih kuantitas (DQ) dan selisih pembulatan (BD) memblokir posting; selisih harga (PP) wajib persetujuan empat mata serta aporsionasi cakupan stok DEC-P2P-013 (1300 Persediaan vs 5200 PPV).",
      od08GovernanceBody:
        "OD-08: Hanya tagihan PO Three-Way Match yang diaktifkan. Pengeluaran Non-PO dan Uang Muka Pemasok ditolak menunggu persetujuan Owner.",
      od07TaxDisclaimer:
        "OD-07: Kode pajak PPN Masukan & PPh 23 berstatus dapat dikonfigurasi; pastikan Faktur Pajak & NPWP lawan transaksi terverifikasi sebelum pelaporan.",
      tabListLabel: "Tampilan modul Hutang Usaha",
      tabs: {
        bills: "Faktur Pemasok",
        grniQueue: "Antrean GRNI Terbuka",
        subledger: "Sub-Buku Besar AP & Jurnal",
        policyTax: "Kebijakan Match & Pajak",
      },
      stats: {
        openPayableLiability: "Saldo Hutang Usaha Terbuka",
        openGrniPool: "Akrual GRNI Menunggu Tagihan",
        pendingApprovalBills: "Menunggu Persetujuan 4-Mata",
        blockedPayables: "Hutang Ditahan Pembayaran",
      },
      refreshButton: "Segarkan",
      newBillButton: "Buat Faktur Pemasok Baru",
      filterSearchLabel: "Cari nomor faktur, PO, atau pemasok",
      filterSearchPlaceholder: "Cari BILL / INV / PO / Pemasok...",
      billsCardTitle: "Daftar Faktur Pemasok & Status Pencocokan 3-Arah",
      emptyBillsTitle: "Belum Ada Faktur Pemasok",
      emptyBillsBody:
        "Buat draf faktur pemasok dari tab Antrean GRNI Terbuka untuk mencocokkan PO, penerimaan Gate 2, dan faktur vendor.",
      grniQueueCardTitle: "Baris Akrual GRNI Terbuka Siap Ditagih (PO Three-Way Match)",
      emptyGrniTitle: "Semua GRNI Telah Ditagih",
      emptyGrniBody:
        "Belum terdapat baris akrual GRNI dengan sisa kuantitas terbuka untuk entitas hukum ini.",
      subledgerCardTitle: "Sub-Buku Besar Hutang Usaha & Revaluasi Harga S09",
      emptyPayablesTitle: "Belum Ada Saldo AP Terbuka",
      emptyPayablesBody: "Item Hutang Usaha muncul secara atomik ketika faktur pemasok yang telah disetujui diposting ke GL.",
      priceAdjustmentsTitle: "Riwayat Penyesuaian Valuasi & Selisih Harga (DEC-P2P-013)",
      emptyPriceAdjustments: "Belum ada pencatatan selisih harga faktur pada entitas ini.",
      policySectionTitle: "Kebijakan Pencocokan 3-Arah Aktif (OD-03 Safe Default)",
      taxTableTitle: "Kode Pajak Aktif untuk Tagihan Pemasok (OD-07)",
      createBillAction: "Tagih Baris Ini",
      composer: {
        titleNew: "Buat & Pratinjau Faktur Pemasok (Match 3-Arah)",
        titleEditPrefix: "Ubah Draf Faktur",
        cancelButton: "Tutup Formulir",
        poSelectLabel: "Pesanan Pembelian (PO) & Pemasok",
        emptyPoOption: "Pilih PO dengan GRNI terbuka...",
        accrualLineSelectLabel: "Baris Akrual Penerimaan (GRNI)",
        emptyAccrualOption: "Pilih baris GRNI...",
        billTypeLabel: "Jenis Tagihan (OD-08)",
        vendorInvoiceLabel: "Nomor Faktur Vendor",
        taxInvoiceLabel: "Nomor Seri Faktur Pajak (Opsional)",
        withholdingSlipLabel: "Ref. Bukti Potong PPh (Opsional)",
        invoiceDateLabel: "Tanggal Faktur",
        dueDateLabel: "Tanggal Jatuh Tempo",
        postingDateLabel: "Tanggal Pratinjau Posting GL",
        declaredGrossLabel: "Total Bruto Faktur Vendor (IDR)",
        syncComputedGrossButton: "Samakan dengan Hitungan",
        billedQtyLabel: "Qty Ditagih (Base)",
        invoiceUnitPriceLabel: "Harga Satuan Faktur (IDR)",
        ppnCodeLabel: "Pajak Masukan (PPN)",
        pphCodeLabel: "Pemotongan (PPh 23)",
        noneOption: "Tanpa Pemotongan PPh",
        selectOptionPlaceholder: "Pilih kode pajak...",
        notesLabel: "Catatan Faktur",
        previewButton: "Pratinjau Match 3-Arah & Dampak GL",
        previewBusy: "Menghitung...",
        saveDraftButton: "Simpan Draf Faktur",
        updateDraftButton: "Simpan Perubahan Draf",
        saveBusy: "Menyimpan...",
        previewTitle: "Hasil Pratinjau Pencocokan 3-Arah & Split DEC-P2P-013",
        comparisonTitle: "Perbandingan Baris: PO · GRNI Terbuka · Faktur",
        colOrderedAcceptedOpen: "PO · Sisa GRNI · Stok Pool",
        colBilledPrice: "Qty & Harga Faktur",
        colVarianceSplit: "Selisih & Split 1300/5200",
        colTaxes: "Pajak PPN / PPh",
        journalPreviewTitle: "Pratinjau Jurnal GL Atomik TX-BILL",
        colJournalSide: "Posisi",
        colJournalAccount: "Akun GL",
        colJournalAmount: "Nominal (IDR)",
        sideDebit: "Debit",
        sideCredit: "Kredit",
        coveredStockLabel: "Porsi Stok Ada (Ke Persediaan 1300)",
        uncoveredPpvLabel: "Porsi Sudah Terpakai (Ke PPV 5200)",
        projectedCostLabel: "Proyeksi HPP Baru",
        bdDiffLabel: "Selisih Bruto (BD)",
        netApLabel: "Net AP Dibayar (2100)",
        stalePreviewNotice: "Nilai masukan berubah. Klik Pratinjau ulang sebelum menyimpan atau memposting.",
      },
      detail: {
        sectionTitle: "Detail Faktur Pemasok, Jejak Audit & Kontrol AP",
        closeButton: "Tutup Detail",
        headerRef: "Nomor & Jenis",
        headerVendor: "Pemasok & Jadwal",
        headerDates: "Faktur / Jatuh Tempo",
        headerMatchStatus: "Status Match 3-Arah",
        headerPaymentBlock: "Status Blokir Pembayaran",
        headerTotals: "Ringkasan Nilai (IDR)",
        colAction: "Aksi",
        openDetailButton: "Buka Detail",
        activeDetailButton: "Detail Terbuka",
        colLineVarianceOutcome: "Selisih & Outcome Match",
        editDraftButton: "Ubah Draf Faktur",
        blockingReasonsTitle: "Alasan Pemblokiran Pencocokan 3-Arah",
        linesTitle: "Rincian Baris Faktur & Outcome Match",
        adjustmentsTitle: "Catatan Aporsionasi Stok DEC-P2P-013 & Revaluasi",
        taxesTitle: "Determinasi Pajak Faktur (OD-07 Append-Only)",
        evaluationsTitle: "Riwayat Evaluasi Pencocokan (Audit)",
        submitButton: "Ajukan Persetujuan 4-Mata",
        submitBusy: "Mengajukan...",
        approveButton: "Setujui Faktur",
        rejectButton: "Tolak ke Draf",
        decideBusy: "Memproses keputusan...",
        approveVarianceCheckbox: "Konfirmasi persetujuan selisih harga satuan (PP) terhadap biaya penerimaan GRNI",
        applyBlockCheckbox: "Tahan pembayaran saat diposting (PAYMENT_BLOCKED)",
        decisionReasonLabel: "Alasan Keputusan Persetujuan",
        decisionReasonPlaceholder: "Mis. Selisih harga disetujui sesuai adendum supplier",
        blockReasonLabel: "Alasan Penahanan Pembayaran",
        sodSelfApprovalNotice:
          "Pemisahan Tugas Empat Mata (OD-02): Pengaju faktur tidak boleh menyetujui atau menolak faktur yang diajukannya sendiri.",
        postSectionTitle: "Posting Atomik ke AP, Persediaan & Buku Besar (GL)",
        postingDateLabel: "Tanggal Posting GL",
        postButton: "Posting Faktur ke AP & GL",
        postBusy: "Memposting...",
        blockControlTitle: "Kontrol Blokir / Rilis Pembayaran Hutang Usaha",
        holdPaymentButton: "Tahan Pembayaran",
        releasePaymentButton: "Rilis Blokir Pembayaran",
        blockBusy: "Memperbarui...",
        blockActionReasonLabel: "Alasan Blokir / Rilis",
        noPermSubmitHint: "Membutuhkan izin ap.manage untuk mengajukan faktur.",
        noPermApproveHint: "Membutuhkan izin ap.approve (empat mata) untuk memutuskan persetujuan.",
        noPermPostHint: "Membutuhkan izin ap.post untuk memposting faktur ke AP & GL.",
        defaultApproveReason: "Disetujui setelah verifikasi pencocokan 3-arah",
        defaultRejectReason: "Dikembalikan ke draf untuk perbaikan",
        defaultManualHoldReason: "Ditahan manual saat persetujuan",
        defaultBlockHoldReason: "Pembayaran ditahan menunggu verifikasi dokumen",
        defaultReleaseReason: "Verifikasi selesai; blokir pembayaran dirilis",
      },
      micro: {
        unbilledGrniShort: "Sisa GRNI",
        poolOnHandShort: "Stok Pool",
        onHandBeforeBillShort: "Stok sebelum tagihan",
        coveredShort: "Tertutup",
        uncoveredShort: "Tak tertutup",
        inv1300Short: "1300 Persediaan",
        ppv5200Short: "5200 PPV",
        openShort: "Terbuka",
        dueShort: "Jt Tempo",
        invShort: "Faktur",
        whtShort: "PPh",
        varShort: "Selisih",
        netShort: "Netto",
        macShort: "HPP Rata-Rata",
        ppToleranceShort: "Toleransi PP",
        dqToleranceShort: "Toleransi DQ",
        bdLimitShort: "Batas BD",
        effectiveShort: "Berlaku",
      },
      evaluationStageLabels: {
        DRAFT_PREVIEW: "Pratinjau Draf",
        SUBMIT: "Pengajuan",
        POST_COMMIT: "Posting Atomik GL",
      },
      billStatusLabels: {
        DRAFT: "Draf",
        PENDING_APPROVAL: "Menunggu Persetujuan",
        APPROVED: "Disetujui (Siap Post)",
        POSTED: "Sudah Diposting",
        CANCELLED: "Dibatalkan",
      },
      matchStatusLabels: {
        MATCHED: "Cocok Tepat (Exact Match)",
        WITHIN_TOLERANCE: "Dalam Toleransi",
        APPROVAL_REQUIRED: "Wajib Persetujuan Selisih",
        POSTING_BLOCKED: "Diblokir Sebelum Post",
      },
      dimensionOutcomeLabels: {
        MATCHED: "Cocok",
        WITHIN_TOLERANCE: "Dalam Toleransi",
        VARIANCE_APPROVAL_REQUIRED: "Perlu Persetujuan",
        BLOCKED: "Diblokir",
      },
      paymentBlockLabels: {
        NONE: "Siap Dibayar",
        PAYMENT_BLOCKED: "Pembayaran Ditahan",
        RELEASED: "Blokir Dirilis",
      },
      payableStatusLabels: {
        OPEN: "Terbuka",
        PARTIALLY_PAID: "Dibayar Sebagian",
        PAID: "Lunas",
        REVERSED: "Dibalik",
      },
      taxRoleLabels: {
        PPN_INPUT: "PPN Masukan",
        PPH_WITHHOLDING: "Pemotongan PPh",
      },
      taxFamilyLabels: {
        PPN: "PPN",
        PPH: "PPh",
      },
      errors: {
        od03_unreviewed_tolerance_policy_blocked:
          "Kebijakan OD-03 memblokir perubahan toleransi otomatis PP/DQ/BD sebelum ditinjau Product Owner (ATH-CR-006).",
        od08_non_po_bill_not_supported:
          "Kebijakan OD-08 menolak tagihan Non-PO maupun Uang Muka sebelum kebijakan disetujui Owner.",
        self_approval_forbidden:
          "Pemisahan tugas empat mata (OD-02) melarang pengaju menyetujui pengajuannya sendiri.",
        variance_approval_acknowledgement_required:
          "Centang konfirmasi persetujuan selisih harga sebelum menyetujui/memposting faktur dengan PP variance.",
        bill_must_be_approved_before_post:
          "Faktur pemasok wajib disetujui secara independen sebelum dapat diposting ke AP & GL.",
        three_way_match_blocked:
          "Pencocokan 3-arah memblokir posting (periksa DQ kuantitas, selisih bruto BD, NPWP pajak, atau lantai valuasi OD-04).",
        overbilling_blocked:
          "Kuantitas ditagih melebihi sisa kapasitas GRNI yang belum ditagih (DQ = 0).",
        posting_blocked:
          "Faktur ditandai Diblokir (POSTING_BLOCKED) dan tidak dapat diposting.",
        od04_valuation_floor_breached:
          "Kebijakan OD-04 menolak penyesuaian harga negatif karena akan membuat nilai buku persediaan menjadi nol atau negatif.",
        duplicate_supplier_invoice_number:
          "Nomor faktur vendor sudah terdaftar untuk pemasok yang sama pada entitas ini.",
        period_closed:
          "Periode akuntansi pada tanggal posting berstatus ditutup.",
        insufficient_entity_permission:
          "Izin entitas aktif Anda tidak mencukupi untuk menjalankan tindakan AP ini.",
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
        payables: "Accounts Payable",
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
      availableState: "Available (S01–S07)",
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
        payables: "Accounts Payable, 3-Way Match & Price Diff",
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
      lotPolicyLabel: "Lot / Batch Tracking Policy",
      lotPolicyLabels: {
        NONE: "No Lot Tracking",
        OPTIONAL: "Optional Lot",
        REQUIRED: "Required Lot (S08)",
      },
      updateLotPolicyButton: "Apply Lot Policy",
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
        unlotted_stock_prevents_required_lot_policy:
          "Existing unlotted stock prevents switching this item to required lot tracking.",
        non_stock_item_cannot_enable_lot_tracking: "Non-stock service items cannot enable lot tracking.",
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
      pageTitle: "Dock Custody (Gate 1) & Valued Receiving (Gate 2)",
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
      loadingDetail: "Loading receipt details...",
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
        PARTIALLY_ACCEPTED: "Partially Accepted (Valued)",
        VALUED_ACCEPTED: "Valued & Accepted",
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
      stockStatusLabels: {
        UNRESTRICTED: "Unrestricted Available",
        QUALITY_INSPECTION: "Quality Inspection Hold",
        BLOCKED: "Blocked Inventory",
      },
      gate2: {
        sectionTitle: "Gate 2 Valued Acceptance — Moving-Average Valuation & GRNI",
        od04Notice:
          "OD-04 policy active (ATH-CR-004): valued acceptance atomically posts Dr Inventory Asset / Cr GRNI and recalculates entity moving-average cost with decimal precision.",
        readOnlyPermissionNote:
          "Your role grants do not include receiving.accept in this legal entity; valuation preview remains read-only.",
        acceptanceDateLabel: "Valued Posting Date",
        stockStatusLabel: "Destination Stock Status",
        notesLabel: "Valued Acceptance Notes (Optional)",
        notesPlaceholder: "e.g. Batch 2 accepted into main stockroom",
        colItem: "Line & Item (Base UoM)",
        colReleasedRemaining: "Remaining Released",
        colAcceptQty: "Accept Quantity (Base)",
        previewButton: "Calculate Valuation & GL Preview",
        previewBusy: "Calculating preview...",
        confirmButton: "Confirm Valued Acceptance",
        confirmBusy: "Posting Stock & GRNI...",
        previewStaleNotice:
          "Inputs changed after preview calculation. Recalculate preview before confirming.",
        previewCardTitle: "Moving-Average Recalculation & Balanced GL Preview",
        colPrevCost: "Before Qty / MAC",
        colProjectedCost: "Projected Qty / MAC",
        colAcceptedAmt: "Accepted Value (IDR)",
        journalPreviewTitle: "Balanced Journal Preview (Dr / Cr)",
        sideLabels: {
          DEBIT: "Debit",
          CREDIT: "Credit",
        },
        blockedHeading: "Posting Blocked by Policy (OD-04 / Period / Custody)",
        successPrefix: "Valued acceptance committed permanently:",
        replaySuffix: "(Idempotent replay without duplicate stock or GL postings.)",
        openInventoryLink: "Inspect in Inventory & Valuation",
        linkedSectionTitle: "Linked Gate 2 Valued Acceptances on This GRN",
        emptyLinked: "No Gate 2 valued acceptances posted for this receipt yet.",
        colAcceptanceRef: "Acceptance Ref & Date",
        colLinkedRefs: "Movement, GRNI & GL Journal",
        colAcceptedTotal: "Accepted Qty & Amount (IDR)",
        gate1GlBadgePrefix: "Gate 1 GL:",
        glBadgePrefix: "Gate 2 GL:",
        locationLabel: "Destination Storage Location (Optional)",
        locationAutoOption: "Default Zone for Stock Status",
        lotNumberLabel: "Lot / Batch Number",
        lotNumberPlaceholder: "e.g., LOT-JKT-2611-01",
        lotRequiredBadge: "Lot Required",
        lotDisabledNote: "Lot tracking disabled",
        expiryDateLabel: "Expiry Date (Optional)",
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
        acceptanceLinesRequired:
          "Enter a positive valued acceptance quantity on at least one released line.",
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
        acceptance_lines_required:
          "Provide at least one released receipt line with positive accepted quantity.",
        invalid_stock_status: "Selected inventory destination status is invalid.",
        receipt_fully_rejected_unvalued:
          "Fully rejected Gate 1 receipts have no released balance eligible for Gate 2.",
        non_stocked_item_not_inventory_eligible:
          "Non-stock or service lines cannot enter valued inventory.",
        acceptance_qty_must_be_positive: "Valued acceptance quantity must be positive.",
        acceptance_exceeds_released_quantity:
          "Accepted quantity exceeds Gate 1 released balance; held or rejected quantities cannot enter valued inventory.",
        od04_zero_or_negative_acceptance_value_blocked:
          "OD-04 blocks zero or negative monetary valuation entries.",
        od04_anomalous_valuation_pool_blocked:
          "Item valuation pool is blocked by OD-04 safety checks and requires Product Owner resolution (ATH-CR-004).",
        duplicate_acceptance_idempotency_conflict:
          "This Gate 2 idempotency key already belongs to a different acceptance payload.",
        missing_inventory_asset_mapping:
          "Inventory Asset (1300) GL account mapping is missing in Finance configuration.",
        missing_grni_clearing_mapping:
          "GRNI Clearing (2110) GL account mapping is missing in Finance configuration.",
        period_closed:
          "The fiscal period covering this posting date is CLOSED and refuses accrual journals.",
        period_not_found_for_date:
          "No fiscal period exists covering the chosen posting date.",
        insufficient_entity_permission:
          "Your active role grants do not permit performing this action in this legal entity.",
        concurrency_conflict: "Another transaction modified this record concurrently; reload and retry.",
        lot_required_by_item_policy:
          "This item requires a stock lot number for Gate 2 valued acceptance.",
        lot_not_enabled_for_item: "Lot tracking is disabled on this catalog item.",
        location_zone_mismatch:
          "Selected destination location zone does not match the chosen stock status.",
        location_not_found: "Warehouse location was not found in this legal entity.",
        location_inactive: "Selected warehouse location is inactive.",
      },
    },
    inventory: {
      pageTitle: "Inventory, Moving-Average Valuation & Open GRNI",
      backToOverview: "Entity Overview",
      loadErrorTitle: "Unable to load entity inventory ledger",
      retryButton: "Reload Inventory Ledger",
      od04PolicyBanner:
        "OD-04 Policy Active (ATH-CR-004): per-entity moving-average valuation in functional currency (IDR); Quality Inspection and Blocked buckets retain carrying value while keeping zero fulfillment availability.",
      od10PolicyBanner:
        "OD-10 Policy Active (ATH-CR-005): negative stock and over-reservation are strictly blocked; intra-warehouse transfers and quality dispositions conserve stock and carrying value without extra GL journals.",
      tabListLabel: "Inventory module views",
      tabs: {
        positions: "Stock & Moving-Average Valuation",
        inspections: "Quality Inspections",
        accruals: "Open GRNI Accruals",
        movements: "Recent Stock Movements",
      },
      stats: {
        stockedItemsWithBalance: "Stocked Items in Pool",
        carryingValueTotal: "Total Carrying Value",
        openGrniTotal: "Open GRNI Liability",
        restrictedItemCount: "Items with Restricted Stock",
      },
      filterSearchLabel: "Search SKU, GRNI reference, or PO",
      filterSearchPlaceholder: "Filter SKU, GRNI ref, or PO...",
      filterStatusLabel: "Filter by stock bucket",
      allStockStatuses: "All Stock Buckets",
      refreshButton: "Refresh",
      positionsCardTitle: "Perpetual Stock Balances & Moving-Average Cost Pools",
      emptyPositionsTitle: "No Valued Inventory Balances Yet",
      emptyPositionsBody:
        "Confirm a Gate 2 valued acceptance in Dock Receiving to initialize stock positions and moving-average valuation pools.",
      colItem: "Item Code & Name",
      colBuckets: "Stock Status & Available",
      colValuedQty: "Total Valued Qty",
      colMovingAverageCost: "Moving-Avg Cost & Carrying Value",
      colPoolStatus: "Pool Status",
      colAction: "Ledger",
      inspectLedgerButton: "Inspect Ledger",
      activeLedgerButton: "Ledger Open",
      bucketCodes: {
        unrestricted: "Unrest.",
        qi: "QI",
        blocked: "Blocked",
        available: "Avail.",
      },
      poolStatusLabels: {
        ACTIVE: "Pool Active",
        BLOCKED: "Blocked (OD-04)",
      },
      stockStatusLabels: {
        UNRESTRICTED: "Unrestricted",
        QUALITY_INSPECTION: "Quality Inspection",
        BLOCKED: "Blocked",
      },
      accrualStatusLabels: {
        OPEN: "Open (Unbilled)",
        PARTIALLY_CLEARED: "Partially Cleared",
        CLEARED: "Cleared",
      },
      movementTypeLabels: {
        PURCHASE_RECEIPT_ACCEPTANCE: "Purchase Receipt Acceptance (Gate 2)",
        LOCATION_TRANSFER: "Warehouse Location Transfer",
        QUALITY_STATUS_CHANGE: "Quality Status / Hold Transition",
        OPERATIONAL_ISSUE: "Operational Stock Issue",
      },
      entryTypeLabels: {
        RECEIPT_ACCEPTANCE: "Valued Acceptance",
        OPERATIONAL_ISSUE: "Operational Stock Issue",
        INVOICE_PRICE_ADJUSTMENT: "Invoice Price Adjustment (S09)",
      },
      accrualsCardTitle: "GRNI Accrual Subledger (Pending S09 Three-Way Match)",
      emptyAccrualsTitle: "No GRNI Accruals Yet",
      emptyAccrualsBody:
        "Open GRNI accrual records appear atomically when Gate 2 valued receipt acceptances post.",
      colAccrualRef: "GRNI Ref & Date",
      colSourceChain: "Source Chain (Gate 2 / GRN / PO / GL)",
      colSupplier: "Supplier",
      colOpenVsAccrued: "Remaining Open & Accrued",
      colAccrualStatus: "Accrual Status",
      movementsCardTitle: "Append-Only Stock Movement History",
      emptyMovementsTitle: "No Stock Movements Recorded",
      emptyMovementsBody: "No inventory stock movements exist in this legal entity yet.",
      colMovementRef: "Movement Ref & Date",
      colMovementType: "Type & Source Document",
      colMovementQtyVal: "Quantity & Valuation (IDR)",
      colMovementActor: "Posted By",
      ledgerCardTitle: "Item Moving-Average Valuation Ledger",
      loadingLedger: "Loading valuation ledger...",
      selectItemPrompt: "Select any item row above to inspect sequential moving-average recalculations and linked GL journals.",
      emptyLedgerEntries: "No valuation ledger entries exist for this item yet.",
      colLedgerSeq: "Seq",
      colLedgerSource: "Date, Source & GL Journal",
      colLedgerDelta: "Qty Delta · Unit Cost · Value",
      colLedgerBeforeAfterCost: "Before → After MAC",
      colLedgerCarrying: "Ending Qty & Carrying",
      openReceivingLink: "Open Dock Receiving",
      zoneTypeLabels: {
        RECEIVING_STAGING: "Receiving Staging",
        STORAGE: "Storage Rack",
        QUARANTINE: "Quality Quarantine",
        BLOCKED_HOLD: "Blocked Hold Area",
        DISPATCH: "Dispatch Staging",
      },
      lotPolicyLabels: {
        NONE: "No Lot Tracking",
        OPTIONAL: "Optional Lot",
        REQUIRED: "Required Lot",
      },
      inspectionStatusLabels: {
        PENDING: "Pending Inspection",
        PARTIALLY_DECIDED: "Partially Decided",
        COMPLETED: "Completed",
      },
      reservationStatusLabels: {
        ACTIVE: "Active",
        PARTIALLY_CONSUMED: "Partially Consumed",
        FULFILLED: "Fulfilled",
        RELEASED: "Released",
      },
      reservationSourceLabels: {
        SALES_ORDER: "Sales Order",
        MANUAL_HOLD: "Manual Hold",
        FULFILLMENT: "Fulfillment Allocation",
      },
      qualityReasonLabels: {
        PASSED_SPECIFICATION: "Passed Specification Check",
        DAMAGED_OR_CONTAMINATED: "Damaged or Contaminated",
        SPEC_DEVIATION_HOLD: "Specification Deviation Hold",
        MISSING_CERTIFICATE: "Missing Certificate / COA",
        SUPERVISOR_REEVALUATION: "Supervisor Re-Evaluation",
      },
      inspectionsTab: {
        cardTitle: "Receipt Quality Inspection Queue (Gate 2 Quarantine)",
        emptyTitle: "No Quality Inspections Queued",
        emptyBody: "Gate 2 acceptances posted into Quality Inspection appear here automatically for release or block disposition.",
        colInspectionRef: "Inspection Ref & Source",
        colSourceLot: "Item, Quarantine Zone & Lot",
        colQuantities: "Quantity Breakdown (Pending / Released / Blocked)",
        colStatusAction: "Status & Follow-Up",
        openItemButton: "Open Stock Detail & Dispose",
        releasedPrefix: "Released",
        blockedPrefix: "Blocked",
        pendingPrefix: "Pending",
      },
      detailSheet: {
        sectionTitle: "Stock Availability, Locations, Lots & Operations",
        loadingDetail: "Loading location balances, lots, inspections, and reservations...",
        selectPrompt: "Select any item row above to inspect locations, lots, quality dispositions, and stock reservations.",
        reconciledBadge: "Reconciled",
        unreconciledBadge: "Reconciliation Attention Needed",
        lotPolicyPrefix: "Lot Policy",
        metricTotalValued: "Total Valued On-Hand",
        metricUnrestricted: "Unrestricted",
        metricQualityInspection: "Quarantined (QI)",
        metricBlocked: "Blocked",
        metricReserved: "Reserved",
        metricAvailable: "Fulfillment Available",
        subTabOperations: "Locations, Lots & Operations",
        subTabTimeline: "State & Movement Timeline",
        balancesTitle: "Balances by Location, Lot & Status",
        emptyBalances: "No location balance rows recorded for this item yet.",
        colLocationZone: "Warehouse & Location (Zone)",
        colLotExpiry: "Lot Number & Expiry",
        colStockStatus: "Stock Status",
        colOnHandReservedAvailable: "On-Hand · Reserved · Available",
        operationsTitle: "Inventory & Quality Operations",
        qualityReadOnlyNotice:
          "Quality read-only mode: your role holds warehouse operations (inventory.manage) without quality.inspect rights, keeping release and hold actions locked.",
        warehouseManageReadOnlyNotice:
          "Read-only mode: your role does not hold inventory.manage in this entity to transfer stock, reserve inventory, or create locations.",
        dispositionCardTitle: "Record Quality Inspection Disposition",
        inspectionSelectLabel: "Queued Inspection Record",
        releaseQtyLabel: "Release Qty to Unrestricted",
        releaseLocationLabel: "Release Storage Location",
        blockQtyLabel: "Block Qty",
        blockLocationLabel: "Blocked Hold Location",
        reasonCodeLabel: "Quality Reason Code",
        evidenceRefLabel: "COA / Evidence Ref (Optional)",
        notesLabel: "Disposition / Movement Notes (Optional)",
        submitDispositionBusy: "Recording Disposition...",
        submitDispositionButton: "Record Quality Disposition",
        dispositionSuccessPrefix: "Quality disposition recorded via movement",
        invalidDispositionQtyError: "Enter a non-negative release and/or block quantity greater than zero.",
        invalidPositiveQtyError: "Enter a positive quantity greater than zero.",
        noPendingInspections: "No pending quarantine inspections exist on this item.",
        transferCardTitle: "Intra-Warehouse Location Transfer (Carrying Value Preserved)",
        sourceBalanceLabel: "Source Location Balance",
        emptyMovableBalancesOption: "No stocked location balances available",
        targetLocationLabel: "Target Location (Same Warehouse)",
        emptyTransferTargetOption: "No compatible target location in warehouse",
        transferQtyLabel: "Transfer Quantity",
        postingDateLabel: "Posting Date",
        submitTransferBusy: "Transferring Stock...",
        submitTransferButton: "Transfer Location",
        transferSuccessPrefix: "Stock transferred via movement",
        statusTransitionCardTitle: "Manual Stock Hold / Unblock (Quality)",
        fromStatusLabel: "From Status",
        toStatusLabel: "To Status",
        fromLocationLabel: "Source Location",
        toLocationLabel: "Target Location",
        statusQtyLabel: "Transition Quantity",
        submitStatusBusy: "Applying Status Change...",
        submitStatusButton: "Apply Status Transition",
        statusSuccessPrefix: "Stock status updated via movement",
        reservationCardTitle: "Create Explicit Stock Reservation (OD-10)",
        resLocationLabel: "Unrestricted Location",
        emptyReserveBalanceOption: "No unrestricted stock available to reserve",
        resLotLabel: "Lot (If Applicable)",
        resSourceTypeLabel: "Source Type",
        resSourceDocLabel: "Source Document Ref",
        resQtyLabel: "Reserve Quantity",
        resNotesLabel: "Reservation Notes (Optional)",
        submitReserveBusy: "Reserving Stock...",
        submitReserveButton: "Commit Stock Reservation",
        reserveSuccessPrefix: "Reservation created:",
        reservationsTableTitle: "Active Reservations & Release History",
        emptyReservations: "No reservations recorded for this item yet.",
        releaseQtyPlaceholder: "Blank = full remaining",
        releaseButton: "Release Qty",
        releaseAllButton: "Release All",
        releaseBusy: "Releasing...",
        colResRef: "Reservation Ref & Date",
        colResSource: "Source & Location / Lot",
        colResQty: "Reserved · Released · Remaining",
        colResStatus: "Status & Actions",
        warehouseToggleLabel: "Manage Warehouses & Storage Locations",
        warehouseCodeLabel: "Warehouse Code",
        warehouseNameLabel: "Warehouse Name",
        createWarehouseButton: "Add Warehouse",
        warehouseSelectLabel: "Warehouse",
        locationCodeLabel: "Location Code",
        locationNameLabel: "Location Name",
        zoneTypeLabel: "Location Zone",
        defaultReceivingLabel: "Default receiving zone",
        createLocationButton: "Save New Location",
        createBusy: "Saving...",
        warehouseCreatedMsg: "Created warehouse:",
        locationCreatedMsg: "Created warehouse location:",
        timelineTitle: "Append-Only Location, Quality & Valuation Timeline",
        emptyTimeline: "No stock movement lines recorded for this item yet.",
        colTimelineRef: "Movement Ref & Date",
        colTimelineLocationLot: "Location, Zone & Lot",
        colTimelineDelta: "Status & Quantity Delta",
        colTimelineCost: "Unit Cost · Valuation Delta",
        issueCardTitle: "Operational Stock Issue (Dr 5100 COGS / Cr 1300 Inventory)",
        issueHint:
          "Issue unreserved unrestricted stock at current entity moving-average unit cost.",
        issueSourceLabel: "Issue Reference",
        issueLocationLabel: "Location & Lot Balance",
        issueQtyLabel: "Issue Quantity",
        issueNotesLabel: "Issue Notes",
        issueSubmitButton: "Post Operational Issue",
        issueSubmitBusy: "Posting issue...",
        issueSuccessPrefix: "Operational issue posted:",
        issueLotSelectLabel: "Lot",
        noLotOption: "No Lot",
        emptyIssueBalanceOption: "No unreserved unrestricted stock balance available to issue.",
      },
      errors: {
        od10_insufficient_available_stock:
          "OD-10 policy rejected operation: insufficient unreserved unrestricted stock available.",
        od10_negative_stock_forbidden:
          "OD-10 policy forbids negative stock on any location balance or entity position.",
        od10_restricted_stock_not_reservable:
          "Only unrestricted stock may be reserved for fulfillment.",
        pending_receipt_inspection_location_locked:
          "Quarantine stock is locked by an open receipt inspection; use Quality Disposition to release or block.",
        location_zone_mismatch:
          "Target location zone is incompatible with the selected stock status.",
        disposition_exceeds_pending_quantity:
          "Release plus block quantity exceeds the inspection's remaining pending quantity.",
        duplicate_warehouse_code: "Warehouse code already exists in this legal entity.",
        duplicate_location_code: "Location code already exists in this legal entity.",
        duplicate_disposition_idempotency_conflict:
          "Disposition idempotency key already belongs to a different payload.",
        duplicate_reservation_idempotency_conflict:
          "Reservation idempotency key already belongs to a different payload.",
        duplicate_movement_idempotency_conflict:
          "Movement idempotency key already belongs to a different payload.",
        insufficient_entity_permission:
          "Your active role grants do not permit performing this action in this legal entity.",
        period_closed:
          "The fiscal period for this posting date is CLOSED.",
      },
    },
    payables: {
      pageTitle: "Accounts Payable, Three-Way Match & Price Difference",
      backToOverview: "Entity Overview",
      loadErrorTitle: "Unable to load entity Accounts Payable workspace",
      retryButton: "Reload AP Data",
      accessDeniedTitle: "Accounts Payable Access Denied",
      accessDeniedBody:
        "Your active legal-entity grants do not include permission to read or manage supplier bills (ap.read).",
      readOnlyBanner:
        "Read-only mode: your role holds ap.read without edit, four-eyes variance approval, or GL posting rights.",
      od03GovernanceTitle: "Active OD-03 & OD-08 Governance (ATH-CR-006)",
      od03GovernanceBody:
        "Automatic PP/DQ/BD tolerances = 0 (Exact Match Default). Quantity overbilling (DQ) and header balance differences (BD) block posting; price variances (PP) require four-eyes approval and DEC-P2P-013 stock-coverage apportionment (1300 Inventory vs 5200 PPV).",
      od08GovernanceBody:
        "OD-08: Only PO Three-Way Match supplier bills are active. Non-PO expense and supplier advance bills are blocked pending Product Owner approval.",
      od07TaxDisclaimer:
        "OD-07: Indonesian PPN Input & PPh 23 tax rules are configurable; ensure Faktur Pajak and counterparty NPWP are verified before filing.",
      tabListLabel: "Accounts Payable views",
      tabs: {
        bills: "Supplier Bills",
        grniQueue: "Open GRNI Queue",
        subledger: "AP Subledger & Journals",
        policyTax: "Match & Tax Policy",
      },
      stats: {
        openPayableLiability: "Open AP Liability",
        openGrniPool: "Unbilled GRNI Pool",
        pendingApprovalBills: "Pending 4-Eyes Approval",
        blockedPayables: "Payment-Blocked Items",
      },
      refreshButton: "Refresh",
      newBillButton: "New Supplier Bill",
      filterSearchLabel: "Search bill, vendor invoice, PO, or supplier",
      filterSearchPlaceholder: "Search BILL / INV / PO / Supplier...",
      billsCardTitle: "Supplier Bills & Three-Way Match Register",
      emptyBillsTitle: "No Supplier Bills Recorded",
      emptyBillsBody:
        "Create a supplier bill draft from the Open GRNI Queue tab to match PO commitment, Gate 2 receipt, and vendor invoice.",
      grniQueueCardTitle: "Open GRNI Accrual Lines Eligible for Billing (PO Three-Way Match)",
      emptyGrniTitle: "All GRNI Accruals Billed",
      emptyGrniBody:
        "No unbilled accepted GRNI accrual lines remain in this legal entity.",
      subledgerCardTitle: "Accounts Payable Subledger & S09 Valuation Revaluations",
      emptyPayablesTitle: "No Open Payable Items Yet",
      emptyPayablesBody: "AP subledger liabilities appear atomically when an approved supplier bill is posted.",
      priceAdjustmentsTitle: "Invoice Price Adjustments & Stock-Coverage Apportionment (DEC-P2P-013)",
      emptyPriceAdjustments: "No invoice price adjustments have posted in this entity yet.",
      policySectionTitle: "Active Three-Way Match Policy (OD-03 Safe Default)",
      taxTableTitle: "Active Supplier Tax Codes (OD-07 Configurable)",
      createBillAction: "Bill This Receipt",
      composer: {
        titleNew: "Draft & Preview Supplier Bill (Three-Way Match)",
        titleEditPrefix: "Edit Supplier Bill Draft",
        cancelButton: "Close Composer",
        poSelectLabel: "Purchase Order (PO) & Supplier",
        emptyPoOption: "Select PO with open GRNI...",
        accrualLineSelectLabel: "GRNI Acceptance Line",
        emptyAccrualOption: "Select open GRNI line...",
        billTypeLabel: "Bill Scope (OD-08)",
        vendorInvoiceLabel: "Vendor Invoice Number",
        taxInvoiceLabel: "Tax Invoice / Faktur Pajak (Optional)",
        withholdingSlipLabel: "Bukti Potong PPh Ref (Optional)",
        invoiceDateLabel: "Invoice Date",
        dueDateLabel: "Due Date",
        postingDateLabel: "GL Preview Posting Date",
        declaredGrossLabel: "Vendor Declared Gross Amount (IDR)",
        syncComputedGrossButton: "Sync Computed Gross",
        billedQtyLabel: "Billed Qty (Base)",
        invoiceUnitPriceLabel: "Invoice Unit Price (IDR)",
        ppnCodeLabel: "Input VAT (PPN)",
        pphCodeLabel: "Withholding (PPh 23)",
        noneOption: "No Withholding",
        selectOptionPlaceholder: "Select tax code...",
        notesLabel: "Bill Notes",
        previewButton: "Run Live 3-Way Match Preview",
        previewBusy: "Computing match...",
        saveDraftButton: "Save Supplier Bill Draft",
        updateDraftButton: "Update Supplier Bill Draft",
        saveBusy: "Saving...",
        previewTitle: "Three-Way Match Preview & DEC-P2P-013 Coverage Split",
        comparisonTitle: "Line Comparison: PO · Open GRNI · Vendor Invoice",
        colOrderedAcceptedOpen: "PO · Open GRNI · Pool Stock",
        colBilledPrice: "Billed Qty & Price",
        colVarianceSplit: "Variance & 1300/5200 Split",
        colTaxes: "PPN / PPh 23 Taxes",
        journalPreviewTitle: "Atomic GL Posting Preview (TX-BILL)",
        colJournalSide: "Side",
        colJournalAccount: "GL Account",
        colJournalAmount: "Amount (IDR)",
        sideDebit: "Debit",
        sideCredit: "Credit",
        coveredStockLabel: "Covered Stock Share (To Inventory 1300)",
        uncoveredPpvLabel: "Uncovered Issued Share (To PPV 5200)",
        projectedCostLabel: "Projected New Moving Avg",
        bdDiffLabel: "Balance Diff (BD)",
        netApLabel: "Net AP Payable (2100)",
        stalePreviewNotice: "Inputs changed. Run preview again before saving or posting.",
      },
      detail: {
        sectionTitle: "Supplier Bill Detail, Audit Evaluations & AP Controls",
        closeButton: "Close Detail",
        headerRef: "Bill & Type",
        headerVendor: "Supplier & Schedule",
        headerDates: "Invoice / Due Date",
        headerMatchStatus: "3-Way Match Status",
        headerPaymentBlock: "Payment Block Status",
        headerTotals: "Financial Totals (IDR)",
        colAction: "Action",
        openDetailButton: "Open Detail",
        activeDetailButton: "Detail Open",
        colLineVarianceOutcome: "Price Diff & Match Outcomes",
        editDraftButton: "Edit Draft",
        blockingReasonsTitle: "Three-Way Match Blocking Reasons",
        linesTitle: "Billed Lines & Match Dimension Outcomes",
        adjustmentsTitle: "DEC-P2P-013 Stock Coverage Split & Pool Revaluations",
        taxesTitle: "Tax Determinations (OD-07 Append-Only)",
        evaluationsTitle: "Three-Way Match Evaluation Audit Trail",
        submitButton: "Submit for 4-Eyes Approval",
        submitBusy: "Submitting...",
        approveButton: "Approve Bill",
        rejectButton: "Reject to Draft",
        decideBusy: "Recording decision...",
        approveVarianceCheckbox: "Confirm four-eyes approval of unit price variance (PP) against GRNI acceptance cost",
        applyBlockCheckbox: "Place payment hold on posting (PAYMENT_BLOCKED)",
        decisionReasonLabel: "Approval Decision Reason",
        decisionReasonPlaceholder: "e.g. Price increase verified against supplier addendum",
        blockReasonLabel: "Payment Block Reason",
        sodSelfApprovalNotice:
          "Four-Eyes Segregation (OD-02): The bill submitter cannot approve or reject their own submission.",
        postSectionTitle: "Atomic AP, Inventory & GL Posting (TX-BILL)",
        postingDateLabel: "GL Posting Date",
        postButton: "Post Bill to AP & GL",
        postBusy: "Posting...",
        blockControlTitle: "Payable Payment Hold / Release Control",
        holdPaymentButton: "Hold Payment",
        releasePaymentButton: "Release Payment Block",
        blockBusy: "Updating...",
        blockActionReasonLabel: "Hold / Release Reason",
        noPermSubmitHint: "Requires ap.manage to submit supplier bill drafts.",
        noPermApproveHint: "Requires independent ap.approve (four-eyes) to approve or reject.",
        noPermPostHint: "Requires ap.post to post supplier bills into AP & GL.",
        defaultApproveReason: "Approved after 3-way match verification",
        defaultRejectReason: "Returned to draft for revision",
        defaultManualHoldReason: "Manual hold upon approval",
        defaultBlockHoldReason: "Payment placed on hold pending verification",
        defaultReleaseReason: "Verification completed; payment hold released",
      },
      micro: {
        unbilledGrniShort: "Unbilled GRNI",
        poolOnHandShort: "Pool On-Hand",
        onHandBeforeBillShort: "On-hand before bill",
        coveredShort: "Covered",
        uncoveredShort: "Uncovered",
        inv1300Short: "1300 Inv",
        ppv5200Short: "5200 PPV",
        openShort: "Open",
        dueShort: "Due",
        invShort: "Inv",
        whtShort: "WHT",
        varShort: "Var",
        netShort: "Net",
        macShort: "MAC",
        ppToleranceShort: "PP Tolerance",
        dqToleranceShort: "DQ Tolerance",
        bdLimitShort: "BD Limit",
        effectiveShort: "Eff.",
      },
      evaluationStageLabels: {
        DRAFT_PREVIEW: "Draft Preview",
        SUBMIT: "Submit",
        POST_COMMIT: "Atomic GL Post",
      },
      billStatusLabels: {
        DRAFT: "Draft",
        PENDING_APPROVAL: "Pending Approval",
        APPROVED: "Approved (Ready to Post)",
        POSTED: "Posted",
        CANCELLED: "Cancelled",
      },
      matchStatusLabels: {
        MATCHED: "Exact Match",
        WITHIN_TOLERANCE: "Within Tolerance",
        APPROVAL_REQUIRED: "Approval Required",
        POSTING_BLOCKED: "Posting Blocked",
      },
      dimensionOutcomeLabels: {
        MATCHED: "Matched",
        WITHIN_TOLERANCE: "Within Tolerance",
        VARIANCE_APPROVAL_REQUIRED: "Approval Required",
        BLOCKED: "Blocked",
      },
      paymentBlockLabels: {
        NONE: "Payable / Clear",
        PAYMENT_BLOCKED: "Payment Blocked",
        RELEASED: "Released",
      },
      payableStatusLabels: {
        OPEN: "Open",
        PARTIALLY_PAID: "Partially Paid",
        PAID: "Paid",
        REVERSED: "Reversed",
      },
      taxRoleLabels: {
        PPN_INPUT: "PPN Input",
        PPH_WITHHOLDING: "PPh Withholding",
      },
      taxFamilyLabels: {
        PPN: "PPN",
        PPH: "PPh",
      },
      errors: {
        od03_unreviewed_tolerance_policy_blocked:
          "OD-03 blocks non-zero PP/DQ/BD automatic tolerances pending Product Owner policy approval (ATH-CR-006).",
        od08_non_po_bill_not_supported:
          "OD-08 rejects Non-PO and supplier advance bills pending Owner approval.",
        self_approval_forbidden:
          "Four-eyes Segregation of Duties (OD-02) forbids approving your own submission.",
        variance_approval_acknowledgement_required:
          "Confirm the price variance approval acknowledgement checkbox before approving or posting.",
        bill_must_be_approved_before_post:
          "Supplier bill must be independently approved before posting into AP & GL.",
        three_way_match_blocked:
          "Three-way match blocked posting (check DQ quantity, BD balance difference, tax NPWP, or OD-04 floor).",
        overbilling_blocked:
          "Billed quantity exceeds remaining unbilled accepted GRNI capacity (DQ = 0).",
        posting_blocked:
          "Bill match status is POSTING_BLOCKED and cannot be posted.",
        od04_valuation_floor_breached:
          "OD-04 blocks negative price variance adjustment that would drive inventory carrying value to zero or negative.",
        duplicate_supplier_invoice_number:
          "This supplier invoice number already exists for the selected supplier in this legal entity.",
        period_closed:
          "The fiscal period on the selected posting date is CLOSED.",
        insufficient_entity_permission:
          "Your active role grants do not permit performing this AP action in this legal entity.",
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

export function formatIdr(amount: number | string): string {
  const numeric = typeof amount === "number" ? amount : Number(amount || 0);
  return new Intl.NumberFormat("id-ID", {
    style: "currency",
    currency: "IDR",
    maximumFractionDigits: 2,
  }).format(Number.isFinite(numeric) ? numeric : 0);
}

export function formatUnitCostIdr(amount: number | string): string {
  const numeric = typeof amount === "number" ? amount : Number(amount || 0);
  return new Intl.NumberFormat("id-ID", {
    style: "currency",
    currency: "IDR",
    minimumFractionDigits: 0,
    maximumFractionDigits: 6,
  }).format(Number.isFinite(numeric) ? numeric : 0);
}
