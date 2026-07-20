export interface Message{
    id?: number;
    fromId: string;
    fromUser: any;
    toId: string;
    toUser:any;
    content: string;
    date: Date;
    isRead: boolean;
}
