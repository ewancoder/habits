import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { DayStatus, StreaksService } from '../streaks.service';
import { ThemeService } from '../theme.service';

@Component({
    selector: 'hab-streak-day',
    imports: [],
    templateUrl: './streak-day.component.html',
    styleUrl: './streak-day.component.scss',
    host: {
        '[class]': 'getClass()',
        '(click)': 'toggle(this.day)',
        '[class.today]': 'day.isToday'
    },
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class StreakDayComponent {
    @Input({ required: true }) day!: StreakDay;

    constructor(
        private service: StreaksService,
        protected themeService: ThemeService
    ) {}

    protected toggle(day: StreakDay) {
        if (day.status === 1) {
            this.service.unmark(day);
        } else {
            this.service.mark(day);
        }
    }

    protected getClass() {
        const markedClass = this.getMarkedClass();
        const themeClass = this.getThemeClass();

        return `${markedClass} ${themeClass}`;
    }

    protected getThemeClass() {
        if (this.themeService.isDarkThemeSignal()) {
            return 'tyr-dark-theme-day';
        }

        return '';
    }

    protected getMarkedClass() {
        switch (this.day.status) {
            case DayStatus.Successful:
                return 'marked';
            case DayStatus.Inherited:
                return 'marked-light';
            default:
                return undefined;
        }
    }
}

export interface StreakDay {
    id: number;
    day: number;
    status: DayStatus;
    habit: string;
    isToday?: boolean;
}
