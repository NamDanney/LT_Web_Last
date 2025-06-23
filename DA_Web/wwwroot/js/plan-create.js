

document.addEventListener('DOMContentLoaded', function () {
    console.log("=== PLAN CREATE SCRIPT LOADING - FIXED VERSION ===");

    // --- STATE & CONSTANTS ---
    let currentStep = 1;
    const totalSteps = 4;
    const form = document.querySelector('.create-tour-form');

    // --- DOM ELEMENTS ---
    const prevBtn = document.getElementById('prev-btn');
    const nextBtn = document.getElementById('next-btn');
    const submitBtn = document.getElementById('submit-btn');

    // Form Inputs
    const departureDateInput = document.getElementById('DepartureDate');
    const returnDateInput = document.getElementById('ReturnDate');
    const durationInput = document.getElementById('Duration');
    const imageFileInput = document.getElementById('ImageFile');
    const imagePreviewContainer = document.getElementById('image-preview-container');
    const imagePreview = document.getElementById('image-preview');

    // --- INITIALIZATION ---
    function initialize() {
        console.log("Initializing plan create form...");

        // ✅ CRITICAL FIX: Ensure allLocations is available and valid
        if (typeof allLocations === 'undefined') {
            console.error("❌ CRITICAL: allLocations is not defined!");
            window.allLocations = [];
        } else {
            console.log("✅ allLocations loaded:", allLocations.length, "locations");
            if (allLocations.length > 0) {
                console.log("✅ Sample location:", allLocations[0]);
            }
        }

        showStep(1);
        setupEventListeners();
        initializeDynamicSections();
        console.log("✅ Plan Create script initialized successfully.");
    }

    // --- EVENT LISTENERS SETUP ---
    function setupEventListeners() {
        console.log("Setting up event listeners...");

        if (nextBtn) nextBtn.addEventListener('click', handleNext);
        if (prevBtn) prevBtn.addEventListener('click', handlePrev);
        if (form) form.addEventListener('submit', handleSubmit);

        if (imageFileInput) imageFileInput.addEventListener('change', handleImagePreview);
        if (departureDateInput) departureDateInput.addEventListener('change', handleDateChange);
        if (returnDateInput) returnDateInput.addEventListener('change', handleDateChange);

        console.log("✅ Event listeners set up successfully.");
    }

    // --- NAVIGATION HANDLERS ---
    function handleNext() {
        console.log(`Attempting to move to next step from ${currentStep}`);
        if (validateStep(currentStep) && currentStep < totalSteps) {
            currentStep++;
            showStep(currentStep);
            console.log(`✅ Moved to step ${currentStep}`);
        } else {
            console.warn(`❌ Validation failed for step ${currentStep}`);
        }
    }

    function handlePrev() {
        if (currentStep > 1) {
            currentStep--;
            showStep(currentStep);
            console.log(`Moved back to step ${currentStep}`);
        }
    }

    function handleSubmit(event) {
        console.log("=== FORM SUBMISSION - FIXED VERSION ===");
        console.log("Submit button clicked. Validating final step...");

        // ✅ CRITICAL FIX: Build complete data object for logging
        try {
            const formDataForLog = buildFormDataForLogging();
            console.log('%c--- DỮ LIỆU SẼ ĐƯỢC GỬI ĐI ---', 'color: #1e90ff; font-weight: bold;');
            console.log(JSON.stringify(formDataForLog, null, 2));
        } catch (e) {
            console.error("❌ Lỗi khi tạo dữ liệu để ghi log:", e);
        }

        if (!validateStep(currentStep)) {
            console.error("❌ Final validation failed. Form submission prevented.");
            event.preventDefault();
            return false;
        } else {
            console.log("✅ Final validation passed. Submitting form...");
            // Allow form submission to continue
            return true;
        }
    }

    function buildFormDataForLogging() {
        const formData = {
            Destination: form.elements['Destination']?.value || '',
            DepartureFrom: form.elements['DepartureFrom']?.value || '',
            Duration: form.elements['Duration']?.value || '',
            Description: form.elements['Description']?.value || '',
            Highlights: [],
            Includes: [],
            Excludes: [],
            Notes: [],
            Schedules: []
        };

        // Get highlights
        document.querySelectorAll('#highlights-container input').forEach(input => {
            if (input.value.trim()) formData.Highlights.push(input.value.trim());
        });

        // Get includes
        document.querySelectorAll('#includes-container input').forEach(input => {
            if (input.value.trim()) formData.Includes.push(input.value.trim());
        });

        // Get excludes
        document.querySelectorAll('#excludes-container input').forEach(input => {
            if (input.value.trim()) formData.Excludes.push(input.value.trim());
        });

        // Get notes
        document.querySelectorAll('#notes-container input').forEach(input => {
            if (input.value.trim()) formData.Notes.push(input.value.trim());
        });

        // ✅ CRITICAL FIX: Get schedules with proper validation
        document.querySelectorAll('#schedule-container .itinerary-day').forEach((dayElement) => {
            const schedule = {
                Day: dayElement.querySelector('input[name*=".Day"]')?.value || '',
                Title: dayElement.querySelector('input[name*=".Title"]')?.value || '',
                LocationIds: [],
                Activities: []
            };

            // Get selected location IDs
            const locationSelect = dayElement.querySelector('select[name*=".LocationIds"]');
            if (locationSelect) {
                Array.from(locationSelect.selectedOptions).forEach(option => {
                    const locationId = parseInt(option.value);
                    if (!isNaN(locationId) && locationId > 0) {
                        schedule.LocationIds.push(locationId);
                    }
                });
            }

            // Get activities
            dayElement.querySelectorAll('.activities-container input').forEach(input => {
                if (input.value.trim()) schedule.Activities.push(input.value.trim());
            });

            formData.Schedules.push(schedule);
        });

        return formData;
    }

    // --- UI UPDATE FUNCTIONS ---
    function showStep(stepNumber) {
        console.log(`Showing step ${stepNumber}`);

        // Hide all step contents
        document.querySelectorAll('.step-content').forEach(el => el.style.display = 'none');

        // Show current step
        const currentStepContent = document.querySelector(`.step-content[data-step="${stepNumber}"]`);
        if (currentStepContent) {
            currentStepContent.style.display = 'block';
        } else {
            console.error(`❌ Step content for step ${stepNumber} not found`);
        }

        updateStepperUI(stepNumber);
        updateNavButtons(stepNumber);

        if (stepNumber === 4) {
            updateSummary();
        }
    }

    function updateStepperUI(stepNumber) {
        document.querySelectorAll('.stepper .step').forEach((stepEl, index) => {
            stepEl.classList.remove('active', 'completed');
            if (index + 1 < stepNumber) {
                stepEl.classList.add('completed');
            } else if (index + 1 === stepNumber) {
                stepEl.classList.add('active');
            }
        });
    }

    function updateNavButtons(stepNumber) {
        if (prevBtn) prevBtn.style.display = stepNumber > 1 ? 'inline-block' : 'none';
        if (nextBtn) nextBtn.style.display = stepNumber < totalSteps ? 'inline-block' : 'none';
        if (submitBtn) submitBtn.style.display = stepNumber === totalSteps ? 'inline-block' : 'none';
    }

    // --- VALIDATION ---
    function validateStep(stepNumber) {
        console.log(`Validating step ${stepNumber}`);
        let isValid = true;

        switch (stepNumber) {
            case 1:
                // ✅ CRITICAL FIX: More thorough validation
                const destination = document.getElementById('Destination')?.value;
                const departureFrom = document.getElementById('DepartureFrom')?.value;

                if (!destination || destination.trim() === '') {
                    alert('Vui lòng nhập tên tour.');
                    isValid = false;
                    break;
                }

                if (!departureFrom || departureFrom === '') {
                    alert('Vui lòng chọn điểm khởi hành.');
                    isValid = false;
                    break;
                }

                if (!departureDateInput?.value || !returnDateInput?.value) {
                    alert('Vui lòng chọn ngày khởi hành và ngày kết thúc.');
                    isValid = false;
                    break;
                }

                if (new Date(returnDateInput.value) < new Date(departureDateInput.value)) {
                    alert('Ngày kết thúc phải sau hoặc bằng ngày khởi hành.');
                    isValid = false;
                    break;
                }
                break;

            case 2:
                // ✅ FIX: Check description
                const description = document.getElementById('Description')?.value;
                if (!description || description.trim() === '') {
                    alert('Vui lòng nhập mô tả tour.');
                    isValid = false;
                }
                break;

            case 3:
                // ✅ CRITICAL FIX: Check if schedule exists and has content
                const scheduleContainer = document.getElementById('schedule-container');
                if (!scheduleContainer || scheduleContainer.children.length === 0) {
                    alert('Lịch trình trống. Vui lòng quay lại Bước 1 và chọn ngày hợp lệ.');
                    isValid = false;
                    break;
                }

                // Check if each day has at least a title
                let hasValidSchedule = false;
                scheduleContainer.querySelectorAll('.itinerary-day').forEach(day => {
                    const title = day.querySelector('input[name*=".Title"]')?.value;
                    if (title && title.trim() !== '') {
                        hasValidSchedule = true;
                    }
                });

                if (!hasValidSchedule) {
                    alert('Vui lòng nhập tiêu đề cho ít nhất một ngày trong lịch trình.');
                    isValid = false;
                }
                break;

            case 4:
                console.log("Final validation check on step 4.");
                // Final step - no additional validation needed
                break;
        }

        console.log(`Step ${stepNumber} validation result:`, isValid);
        return isValid;
    }

    // --- DYNAMIC CONTENT LOGIC ---
    function handleImagePreview(event) {
        const file = event.target.files[0];
        if (file && imagePreview && imagePreviewContainer) {
            const reader = new FileReader();
            reader.onload = function (e) {
                imagePreview.src = e.target.result;
                imagePreviewContainer.style.display = 'block';
            };
            reader.readAsDataURL(file);
        } else if (imagePreviewContainer) {
            imagePreviewContainer.style.display = 'none';
        }
    }

    function handleDateChange() {
        console.log("Date changed, recalculating duration and schedule...");

        if (!departureDateInput?.value || !returnDateInput?.value) {
            if (durationInput) durationInput.value = '';
            generateSchedule(0);
            return;
        }

        const startDate = new Date(departureDateInput.value);
        const endDate = new Date(returnDateInput.value);

        if (endDate >= startDate) {
            const diffTime = Math.abs(endDate - startDate);
            const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24)) + 1;
            if (durationInput) {
                durationInput.value = `${diffDays} ngày ${diffDays > 1 ? diffDays - 1 : 0} đêm`;
            }
            generateSchedule(diffDays);
            console.log(`✅ Generated schedule for ${diffDays} days`);
        } else {
            if (durationInput) durationInput.value = '';
            generateSchedule(0);
        }
    }

    function generateSchedule(numberOfDays) {
        console.log(`Generating schedule for ${numberOfDays} days`);
        const scheduleContainer = document.getElementById('schedule-container');
        if (!scheduleContainer) {
            console.error("❌ Schedule container not found");
            return;
        }

        scheduleContainer.innerHTML = ''; // Clear existing schedule

        for (let i = 0; i < numberOfDays; i++) {
            addScheduleDay(i);
        }

        console.log(`✅ Added ${numberOfDays} schedule days`);
    }

    function initializeDynamicSections() {
        console.log("Initializing dynamic sections...");

        const defaultIncludes = [
            "Xe du lịch đời mới máy lạnh",
            "Khách sạn tiêu chuẩn 3 sao",
            "Các bữa ăn theo chương trình",
            "Vé tham quan",
            "Bảo hiểm du lịch"
        ];

        const defaultExcludes = [
            "Chi phí cá nhân",
            "Tiền tip cho HDV và tài xế"
        ];

        const defaultNotes = [
            "Mang theo giấy tờ tùy thân",
            "Lịch trình có thể thay đổi tùy theo thời tiết"
        ];

        setupAddButton('add-highlight-btn', 'highlights-container', 'highlight-template');
        setupAddButton('add-include-btn', 'includes-container', 'include-template', defaultIncludes);
        setupAddButton('add-exclude-btn', 'excludes-container', 'exclude-template', defaultExcludes);
        setupAddButton('add-note-btn', 'notes-container', 'note-template', defaultNotes);

        console.log("✅ Dynamic sections initialized");
    }

    function setupAddButton(buttonId, containerId, templateId, defaultItems = []) {
        const addButton = document.getElementById(buttonId);
        const container = document.getElementById(containerId);

        if (!addButton || !container) {
            console.warn(`❌ Button ${buttonId} or container ${containerId} not found`);
            return;
        }

        // Clear container and add default items
        container.innerHTML = '';
        if (defaultItems.length > 0) {
            defaultItems.forEach(itemText => addItem(container, templateId, itemText));
        } else {
            addItem(container, templateId, ""); // Add one empty item if no defaults
        }

        // ✅ FIX: Remove existing event listeners before adding new ones
        addButton.removeEventListener('click', addButton._clickHandler);
        addButton._clickHandler = () => addItem(container, templateId);
        addButton.addEventListener('click', addButton._clickHandler);

        console.log(`✅ Setup add button for ${buttonId}`);
    }

    function addItem(container, templateId, value = '') {
        const template = document.getElementById(templateId);
        if (!template) {
            console.error(`❌ Template ${templateId} not found`);
            return;
        }

        const index = container.children.length;
        const newHtml = template.innerHTML.replace(/__index__/g, index);

        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = newHtml;
        const newElement = tempDiv.firstElementChild;

        // Set value if provided
        const input = newElement.querySelector('input');
        if (input && value) {
            input.value = value;
        }

        container.appendChild(newElement);
        console.log(`✅ Added item to ${templateId} with index ${index}`);
    }

    // ✅ CRITICAL FIX: Global remove function
    window.removeItem = function (button) {
        console.log("Removing item:", button);
        const itemToRemove = button.closest('.highlight-item, .list-item, .activity-item, .itinerary-day');
        if (!itemToRemove) {
            console.warn("❌ Could not find item to remove");
            return;
        }

        const container = itemToRemove.parentElement;
        itemToRemove.remove();
        reindexContainer(container);
        console.log("✅ Item removed and container reindexed");
    }

    function reindexContainer(container) {
        if (!container || !container.children.length) return;

        console.log("Reindexing container:", container.id);

        Array.from(container.children).forEach((item, index) => {
            const isScheduleDay = item.classList.contains('itinerary-day');

            if (isScheduleDay) {
                // Re-index schedule day
                const dayLabel = item.querySelector('.day-label');
                if (dayLabel) dayLabel.textContent = `Ngày ${index + 1}`;

                item.querySelectorAll('input, select').forEach(input => {
                    if (input.name) {
                        const newName = input.name.replace(/Schedules\[\d+\]/, `Schedules[${index}]`);
                        input.name = newName;
                    }
                });

                // Re-index activities within the day
                const activitiesContainer = item.querySelector('.activities-container');
                if (activitiesContainer) {
                    Array.from(activitiesContainer.children).forEach((activityItem, activityIndex) => {
                        const activityInput = activityItem.querySelector('input');
                        if (activityInput && activityInput.name) {
                            activityInput.name = `Schedules[${index}].Activities[${activityIndex}]`;
                        }
                    });
                }
            } else {
                // For simple lists like highlights, includes, etc.
                item.querySelectorAll('input, textarea').forEach(input => {
                    if (input.name) {
                        input.name = input.name.replace(/\[\d+\]/, `[${index}]`);
                    }
                });
            }
        });

        console.log("✅ Container reindexing completed");
    }

    // --- SCHEDULE SPECIFIC LOGIC ---
    function addScheduleDay(dayIndex) {
        const scheduleContainer = document.getElementById('schedule-container');
        const template = document.getElementById('schedule-day-template');

        if (!template || !scheduleContainer) {
            console.error("❌ Schedule template or container not found");
            return;
        }

        const dayNumber = dayIndex + 1;
        let newHtml = template.innerHTML
            .replace(/__dayIndex__/g, dayIndex)
            .replace(/__dayNumber__/g, dayNumber);

        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = newHtml;
        const newDayElement = tempDiv.firstElementChild;

        // ✅ CRITICAL FIX: Populate locations with proper error handling
        const locationSelect = newDayElement.querySelector('select[name*="LocationIds"]');
        const titleInput = newDayElement.querySelector('input[name*="Title"]');

        if (locationSelect && window.allLocations && Array.isArray(window.allLocations)) {
            console.log(`✅ Populating locations for day ${dayNumber}:`, window.allLocations.length);

            try {
                let addedCount = 0;
                window.allLocations.forEach((location, index) => {
                    console.log(`Processing location[${index}]:`, location);

                    // SỬA DÒNG NÀY
                    const locationId = location.id;
                    const locationName = location.name;

                    if (locationId && locationName &&
                        typeof locationId === 'number' &&
                        typeof locationName === 'string' &&
                        locationName.trim() !== '') {

                        const option = new Option(locationName, locationId);
                        locationSelect.add(option);
                        addedCount++;
                        console.log(`  ✅ Added: ${locationName} (ID: ${locationId})`);
                    } else {
                        console.warn(`  ❌ Invalid location[${index}]:`, {
                            Id: locationId,
                            Name: locationName,
                            IdType: typeof locationId,
                            NameType: typeof locationName,
                            FullObject: location
                        });
                    }
                });

                console.log(`✅ Successfully added ${addedCount}/${window.allLocations.length} locations to day ${dayNumber}`);

                if (addedCount === 0) {
                    console.error("❌ No valid locations were added! Check location data structure.");
                }

                // ✅ IMPROVED: Better event listener for location changes
                locationSelect.addEventListener('change', function () {
                    const selectedNames = Array.from(this.selectedOptions).map(opt => opt.text);
                    if (titleInput) {
                        titleInput.value = selectedNames.length > 0
                            ? selectedNames.join(' - ')
                            : `Ngày ${dayNumber}: Vui lòng chọn địa điểm`;
                    }
                });
            } catch (error) {
                console.error("❌ Error populating locations:", error);
            }
        } else {
            console.warn("❌ Locations not available for day", dayNumber, {
                locationSelect: !!locationSelect,
                allLocations: !!window.allLocations,
                isArray: Array.isArray(window.allLocations),
                length: window.allLocations?.length
            });
        }

        // Set default title
        if (titleInput) {
            titleInput.value = `Ngày ${dayNumber}: Vui lòng chọn địa điểm`;
        }

        // Set hidden day field
        const dayInput = newDayElement.querySelector('input[name*=".Day"]');
        if (dayInput) {
            dayInput.value = `Ngày ${dayNumber}`;
        }

        scheduleContainer.appendChild(newDayElement);

        // ✅ FIX: Setup add activity button with proper event handling
        const addActivityBtn = newDayElement.querySelector('.add-activity-btn');
        if (addActivityBtn) {
            addActivityBtn.addEventListener('click', function (event) {
                handleAddActivityClick(event);
            });
        }

        // Add one initial activity
        const activitiesContainer = newDayElement.querySelector('.activities-container');
        if (activitiesContainer) {
            addActivityToContainer(activitiesContainer, dayIndex);
        }

        console.log(`✅ Added schedule day ${dayNumber} with index ${dayIndex}`);
    }

    // ✅ IMPROVED: Add activity handler
    function handleAddActivityClick(event) {
        console.log("Add activity button clicked");
        event.preventDefault();
        event.stopPropagation();

        const dayElement = event.target.closest('.itinerary-day');
        if (!dayElement) {
            console.error("❌ Could not find day element");
            return;
        }

        const scheduleContainer = document.getElementById('schedule-container');
        const dayIndex = Array.from(scheduleContainer.children).indexOf(dayElement);

        const activitiesContainer = dayElement.querySelector('.activities-container');
        if (activitiesContainer) {
            addActivityToContainer(activitiesContainer, dayIndex);
            console.log(`✅ Added activity to day ${dayIndex + 1}`);
        } else {
            console.error("❌ Activities container not found");
        }
    }

    // ✅ IMPROVED: Add activity to container
    function addActivityToContainer(container, dayIndex) {
        if (!container) {
            console.error("❌ Activities container is null");
            return;
        }

        const activityTemplate = document.getElementById('activity-template');
        if (!activityTemplate) {
            console.error("❌ Activity template not found");
            return;
        }

        const activityIndex = container.children.length;

        let newHtml = activityTemplate.innerHTML
            .replace(/__dayIndex__/g, dayIndex)
            .replace(/__activityIndex__/g, activityIndex);

        const tempDiv = document.createElement('div');
        tempDiv.innerHTML = newHtml;
        const newActivity = tempDiv.firstElementChild;

        container.appendChild(newActivity);
        console.log(`✅ Added activity ${activityIndex} to day ${dayIndex}`);
    }

    // ✅ IMPROVED: Summary update
    function updateSummary() {
        console.log("Updating summary...");

        // Update basic info
        const destInput = document.getElementById('Destination');
        const durInput = document.getElementById('Duration');

        // Create summary if it doesn't exist
        let summaryContent = document.getElementById('summary-content');
        if (!summaryContent) {
            console.warn("❌ Summary content not found");
            return;
        }

        let summaryHtml = '<div class="summary-section">';
        summaryHtml += `<h4>Tên tour: ${destInput?.value || 'Chưa nhập'}</h4>`;
        summaryHtml += `<h4>Thời gian: ${durInput?.value || 'Chưa nhập'}</h4>`;
        summaryHtml += '</div>';

        // Add schedule summary
        summaryHtml += '<div class="summary-section"><h4>Lịch trình:</h4><ul>';

        const scheduleContainer = document.getElementById('schedule-container');
        if (scheduleContainer) {
            scheduleContainer.querySelectorAll('.itinerary-day').forEach((day, index) => {
                const titleInput = day.querySelector('input[name*="Title"]');
                const title = titleInput?.value || `Ngày ${index + 1}`;
                summaryHtml += `<li>${title}</li>`;
            });
        }

        summaryHtml += '</ul></div>';
        summaryContent.innerHTML = summaryHtml;

        console.log("✅ Summary updated");
    }

    // --- START INITIALIZATION ---
    initialize();

    console.log("=== PLAN CREATE SCRIPT LOADED SUCCESSFULLY ===");
});